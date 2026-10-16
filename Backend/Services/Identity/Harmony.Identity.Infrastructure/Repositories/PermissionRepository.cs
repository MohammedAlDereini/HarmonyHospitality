using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

public class PermissionRepository : BaseRepository<Permission>, IPermissionRepository
{
    private readonly IdentityDbContext context;

    public PermissionRepository(IdentityDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public override async Task<Permission> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return (await this.context.Permissions
            .FirstOrDefaultAsync(p => p.Id == (Guid)id, cancellationToken))!;
    }

    public IQueryable<Permission> Query()
    {
        return this.context.Permissions.AsNoTracking();
    }

    public async Task<IReadOnlyList<Permission>> OfTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await this.context.Permissions
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Permission>> AddCatalogueAsync(IEnumerable<PermissionCatalogueEntry> entries, CancellationToken cancellationToken)
    {
        // Ids are given on Add (a client-side Guid), so the role can point at them before anything is saved.
        var rows = entries.Select(entry => entry.ToPermission()).ToList();
        await this.context.Permissions.AddRangeAsync(rows, cancellationToken);
        return rows;
    }
}
