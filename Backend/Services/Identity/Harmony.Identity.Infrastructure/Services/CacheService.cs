using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Services;

/// <summary>
/// PropX CacheService over the framework's Redis cache, Public level, so keys land under "Global:" where every service
/// may read them. Two deliberate differences from PropX: writes use SaveAsync (overwrite), because this framework's
/// AddAsync is add-if-absent and a republished role must replace the old set; and a failed write throws, because the
/// database already holds the change and a silent failure would keep a revoked permission alive for the whole TTL.
/// </summary>
public class CacheService : ICacheService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(30);

    private readonly ICache<Harmony.Core.Cache.Providers.Redis> redisCache;
    private readonly IdentityDbContext dbContext;

    public CacheService(IdentityDbContext dbContext, ICache<Harmony.Core.Cache.Providers.Redis> redisCache)
    {
        this.dbContext = dbContext;
        this.redisCache = redisCache;
    }

    public async Task<IReadOnlyCollection<TDto>> GetAsync<TEntity, TDto>(
        string cacheKey,
        Func<IQueryable<TEntity>, IQueryable<TDto>> queryBuilder,
        TimeSpan? expiry = null,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        // 1. Try cache (unless bypassed)
        if (!bypassCache)
        {
            var cachedResponse = await this.redisCache.GetAsync<IReadOnlyCollection<TDto>>(
                cacheKey,
                eCacheAccessLevel.Public,
                cancellationToken);

            if (cachedResponse?.Value != null && cachedResponse.Value.Count > 0)
            {
                return cachedResponse.Value;
            }
        }

        // 2. Fetch from DB
        var query = this.dbContext.Set<TEntity>().AsNoTracking();

        var data = await queryBuilder(query).ToListAsync(cancellationToken);

        // 3. Store in cache
        if (data.Count > 0)
        {
            await this.WriteAsync(cacheKey, data, expiry, cancellationToken);
        }

        return data;
    }

    // =========================
    // ADD (Explicit cache set)
    // =========================
    public async Task AddAsync<TDto>(
        string cacheKey,
        TDto data,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            return;
        }

        await this.WriteAsync(cacheKey, data, expiry, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        string cacheKey,
        CancellationToken cancellationToken = default)
    {
        var result = await this.redisCache.ExistsAsync(cacheKey, eCacheAccessLevel.Public, cancellationToken);
        return result == eCacheOperationResult.Success;
    }

    // =========================
    // DELETE (Remove cache)
    // =========================
    public async Task DeleteAsync(
        string cacheKey,
        CancellationToken cancellationToken = default)
    {
        var result = await this.redisCache.DeleteAsync(
            cacheKey,
            eCacheAccessLevel.Public,
            cancellationToken);

        if (result is not (eCacheOperationResult.Success or eCacheOperationResult.KeyDoesNotExist))
        {
            throw new InvalidOperationException($"Removing cache key '{cacheKey}' failed ({result}).");
        }
    }

    private async Task WriteAsync(string cacheKey, object data, TimeSpan? expiry, CancellationToken cancellationToken)
    {
        var result = await this.redisCache.SaveAsync(
            cacheKey,
            data,
            expiry ?? DefaultExpiry,
            eCacheAccessLevel.Public,
            cancellationToken);

        if (result != eCacheOperationResult.Success)
        {
            throw new InvalidOperationException(
                $"Writing cache key '{cacheKey}' failed ({result}). The database holds the change; the cache does not until the next write or restart.");
        }
    }
}
