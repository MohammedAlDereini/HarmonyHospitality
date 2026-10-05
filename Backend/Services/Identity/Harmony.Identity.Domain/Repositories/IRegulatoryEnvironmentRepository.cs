using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>
/// Reference rows have no Guid id and no tenant, so this is the light contract (unit of work) plus what the handlers need,
/// not the full IRepository.
/// </summary>
public interface IRegulatoryEnvironmentRepository : IRepositoryLight<RegulatoryEnvironment>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<RegulatoryEnvironment?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);

    Task<bool> CountryExistsAsync(string countryCode, CancellationToken cancellationToken);

    Task AddAsync(RegulatoryEnvironment environment, CancellationToken cancellationToken);

    IQueryable<RegulatoryEnvironment> Query();
}
