namespace Harmony.Identity.Infrastructure.Caching;

/// <summary>PropX ICacheSeeder: warms the cache once at startup, after the migrations and before the first request.</summary>
public interface ICacheSeeder
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
