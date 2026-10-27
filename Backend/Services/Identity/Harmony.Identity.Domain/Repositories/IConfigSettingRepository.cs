using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

namespace Harmony.Identity.Domain.Repositories;

/// <summary>The registry of settings. Global rows: no tenant filter applies.</summary>
public interface IConfigSettingRepository : IRepository<ConfigSetting>
{
    /// <summary>Is a setting registered under the key (any case).</summary>
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken);

    /// <summary>The setting, tracked, for a change; null when unknown.</summary>
    Task<ConfigSetting?> FindAsync(string key, CancellationToken cancellationToken);

    Task AddAsync(ConfigSetting setting, CancellationToken cancellationToken);

    /// <summary>Every setting, untracked.</summary>
    IQueryable<ConfigSetting> Query();
}
