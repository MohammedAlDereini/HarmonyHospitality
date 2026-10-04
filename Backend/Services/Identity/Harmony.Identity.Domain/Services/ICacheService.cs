namespace Harmony.Identity.Domain.Services;

/// <summary>PropX ICacheService: the one door from Identity to the shared cache. Keys come from PermissionCacheKeys.</summary>
public interface ICacheService
{
    public Task<IReadOnlyCollection<TDto>> GetAsync<TEntity, TDto>(
        string cacheKey,
        Func<IQueryable<TEntity>, IQueryable<TDto>> queryBuilder,
        TimeSpan? expiry = null,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
        where TEntity : class;

    public Task AddAsync<TDto>(
        string cacheKey,
        TDto data,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);

    /// <summary>True while the key lives in the cache. For throttles and one-time markers.</summary>
    public Task<bool> ExistsAsync(
        string cacheKey,
        CancellationToken cancellationToken = default);

    public Task DeleteAsync(
        string cacheKey,
        CancellationToken cancellationToken = default);
}
