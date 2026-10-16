using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

namespace Harmony.Identity.Domain.Repositories;

public interface IPermissionRepository : IRepository<Permission>
{
    public IQueryable<Permission> Query();

    /// <summary>A tenant's catalogue rows, whatever tenant the caller is in (the birth of a tenant reads another tenant's rows).</summary>
    public Task<IReadOnlyList<Permission>> OfTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Fresh rows from catalogue lines, added to the unit of work (not saved); their ids are known at once.</summary>
    public Task<IReadOnlyList<Permission>> AddCatalogueAsync(IEnumerable<PermissionCatalogueEntry> entries, CancellationToken cancellationToken);
}
