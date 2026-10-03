namespace Harmony.Identity.Infrastructure.Caching;

/// <summary>PropX ICacheSeeder: warms the cache once at startup, after the migrations and before the first request.</summary>
public interface ICacheSeeder
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Republishes everything when the sentinel the seeder leaves behind is gone, which means Redis lost its data.
    /// True when a republish ran. False when nothing was lost, or when Redis cannot be read at all.
    /// </summary>
    Task<bool> HealIfLostAsync(CancellationToken cancellationToken = default);
}
