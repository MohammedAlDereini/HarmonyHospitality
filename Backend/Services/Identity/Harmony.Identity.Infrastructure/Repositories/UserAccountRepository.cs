using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class UserAccountRepository : BaseRepository<HarmonyUser>, IUserAccountRepository
{
    private readonly IdentityDbContext context;

    public UserAccountRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<HarmonyUser> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.UserAccounts
            .FirstOrDefaultAsync(a => a.Id == (Guid)id, cancellationToken))!;
    }

    public IQueryable<HarmonyUser> Query()
    {
        return this.context.UserAccounts.AsNoTracking();
    }

    public async Task<HarmonyUser?> FindServicePrincipalAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToUpperInvariant();

        // The tenant wall is dropped by name and replaced by the explicit tenant: the token endpoint
        // has no call-context tenant to filter on. Nothing else about the row changes.
        return await this.context.UserAccounts
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId && a.IsServicePrincipal && a.NormalizedUserName == normalized,
                cancellationToken);
    }
}
