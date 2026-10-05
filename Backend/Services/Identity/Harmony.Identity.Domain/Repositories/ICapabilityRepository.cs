using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

public interface ICapabilityRepository : IRepositoryLight<Capability>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<Capability?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);

    Task AddAsync(Capability capability, CancellationToken cancellationToken);

    IQueryable<Capability> Query();
}
