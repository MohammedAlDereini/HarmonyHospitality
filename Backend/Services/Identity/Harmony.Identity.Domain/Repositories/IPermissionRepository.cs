using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

namespace Harmony.Identity.Domain.Repositories;

public interface IPermissionRepository : IRepository<Permission>
{
    public IQueryable<Permission> Query();
}
