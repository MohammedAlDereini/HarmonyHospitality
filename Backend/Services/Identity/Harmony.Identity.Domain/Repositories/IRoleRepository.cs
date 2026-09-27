using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

namespace Harmony.Identity.Domain.Repositories;

public interface IRoleRepository : IRepository<Role>
{
    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    public IQueryable<Role> Query();
}