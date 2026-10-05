using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Repositories;

public interface ITimeZoneRepository : IRepositoryLight<TimeZoneRef>
{
    /// <summary>The row, tracked, for a change.</summary>
    Task<TimeZoneRef?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken);

    Task AddAsync(TimeZoneRef timeZone, CancellationToken cancellationToken);

    IQueryable<TimeZoneRef> Query();
}
