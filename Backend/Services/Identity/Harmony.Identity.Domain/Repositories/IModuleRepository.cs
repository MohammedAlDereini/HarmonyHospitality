using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

public interface IModuleRepository : IRepositoryLight<Module>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<Module?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);

    Task AddAsync(Module module, CancellationToken cancellationToken);

    IQueryable<Module> Query();
}
