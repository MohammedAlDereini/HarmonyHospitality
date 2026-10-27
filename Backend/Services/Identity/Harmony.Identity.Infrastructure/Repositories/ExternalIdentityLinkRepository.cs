using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.IdentityLinkModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

/// <summary>Reads name the company login or the customer explicitly: a company sign-in arrives before anybody is signed in.</summary>
public class ExternalIdentityLinkRepository : BaseRepository<ExternalIdentityLink>, IExternalIdentityLinkRepository
{
    private readonly IdentityDbContext context;

    public ExternalIdentityLinkRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<ExternalIdentityLink> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.Links().FirstOrDefaultAsync(l => l.Id == (Guid)id, cancellationToken))!;
    }

    public Task<ExternalIdentityLink?> FindAsync(Guid authorityId, string providerSubject, CancellationToken cancellationToken)
    {
        return this.Links().FirstOrDefaultAsync(l => l.FederatedAuthorityId == authorityId && l.ProviderSubject == providerSubject, cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalIdentityLink>> OfAccountAsync(Guid tenantId, Guid userAccountId, CancellationToken cancellationToken)
    {
        return await this.Links()
            .Where(l => l.TenantId == tenantId && l.UserAccountId == userAccountId)
            .OrderByDescending(l => l.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ExternalIdentityLink> Links()
    {
        return this.context.ExternalIdentityLinks
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Include(l => l.Periods);
    }
}
