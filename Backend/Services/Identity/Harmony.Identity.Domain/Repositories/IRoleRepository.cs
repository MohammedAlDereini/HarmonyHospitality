using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

namespace Harmony.Identity.Domain.Repositories;

public interface IRoleRepository : IRepository<Role>
{
    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>The role with its permission rows, tracked, for a permissions change.</summary>
    public Task<Role?> GetWithPermissionsAsync(Guid id, CancellationToken cancellationToken);

    public IQueryable<Role> Query();
}