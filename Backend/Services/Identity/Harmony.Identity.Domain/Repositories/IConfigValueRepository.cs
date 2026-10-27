using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>
/// The customers' setting values. Walled rows: inside a customer the filter does the work. The resolver's read names the
/// customer explicitly, so the platform can see a customer's hotel through the same door.
/// </summary>
public interface IConfigValueRepository : IRepository<ConfigValue>
{
    /// <summary>The value at a place, tracked, for a change; null when nothing is set there.</summary>
    Task<ConfigValue?> FindAsync(Guid tenantId, ConfigScope place, string key, CancellationToken cancellationToken);

    Task AddAsync(ConfigValue value, CancellationToken cancellationToken);

    void Remove(ConfigValue value);

    /// <summary>
    /// Everything a resolution at one hotel can read, in one query: the customer's values for the keys (all keys when
    /// null) at the customer level, at the given groups, and at the hotel. Untracked.
    /// </summary>
    Task<IReadOnlyList<ConfigValue>> ForHotelAsync(Guid tenantId, Guid propertyId, IReadOnlyCollection<Guid> groupIds, IReadOnlyCollection<string>? keys, CancellationToken cancellationToken);

    /// <summary>The caller's own values, untracked, inside the wall.</summary>
    IQueryable<ConfigValue> Query();
}
