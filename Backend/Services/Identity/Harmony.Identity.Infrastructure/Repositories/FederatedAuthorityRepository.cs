using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

/// <summary>
/// Every read drops the tenant filter by name and names the customer or the domain instead: the platform adds a company
/// login to a customer, and the sign-in page routes an address before anybody is signed in.
/// </summary>
public class FederatedAuthorityRepository : BaseRepository<FederatedAuthority>, IFederatedAuthorityRepository
{
    private readonly IdentityDbContext context;

    public FederatedAuthorityRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<FederatedAuthority> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.Authorities().FirstOrDefaultAsync(a => a.Id == (Guid)id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<FederatedAuthority>> OfTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await this.Authorities()
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.Scheme)
            .ToListAsync(cancellationToken);
    }

    public Task<FederatedAuthority?> FindAsync(Guid tenantId, Guid authorityId, CancellationToken cancellationToken)
    {
        return this.Authorities().FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == authorityId, cancellationToken);
    }

    public Task<bool> HasLocalAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return this.context.FederatedAuthorities
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(a => a.TenantId == tenantId && a.Scheme == AuthorityScheme.Local, cancellationToken);
    }

    public Task<bool> IssuerInUseAsync(string issuer, CancellationToken cancellationToken)
    {
        return this.context.FederatedAuthorities
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(a => a.Scheme != AuthorityScheme.Local && a.Issuer == issuer, cancellationToken);
    }

    public Task<bool> DomainListedAsync(string domain, CancellationToken cancellationToken)
    {
        return this.context.FederatedAuthorityDomains
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(d => d.Domain == domain, cancellationToken);
    }

    public Task<FederatedAuthority?> CoveringAsync(string domain, CancellationToken cancellationToken)
    {
        return this.Authorities()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.IsEnabled && a.Scheme != AuthorityScheme.Local && a.Domains.Any(d => d.Domain == domain), cancellationToken);
    }

    public Task<FederatedAuthority?> ListingAsync(string domain, CancellationToken cancellationToken)
    {
        return this.Authorities()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Scheme != AuthorityScheme.Local && a.Domains.Any(d => d.Domain == domain), cancellationToken);
    }

    public Task<FederatedAuthority?> FindByIssuerAsync(AuthorityScheme scheme, string issuer, CancellationToken cancellationToken)
    {
        return this.Authorities()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Scheme == scheme && a.Issuer == issuer, cancellationToken);
    }

    private IQueryable<FederatedAuthority> Authorities()
    {
        return this.context.FederatedAuthorities
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Include(a => a.Domains);
    }
}
