using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Caching;

/// <summary>
/// Identity's side of the stale-token check: a user's security version comes from the cache, and a miss is answered
/// from the database and written back (PropX: heal a miss with a query). The framework readers in the other services
/// fail closed on a miss; when the miss turns out to be a wiped Redis, everything is republished first, so they heal too.
/// Runs inside token validation, before the call context exists: the database work runs in its own scope so the
/// request's DbContext, which captures the tenant when constructed, is never built too early.
/// </summary>
public sealed class DatabaseBackedSecurityVersionResolver : ISecurityVersionResolver
{
    private readonly ICache<Harmony.Core.Cache.Providers.Redis> redis;
    private readonly IServiceScopeFactory scopes;
    private readonly IMultiTenancyService tenants;
    private readonly ILogger<DatabaseBackedSecurityVersionResolver> logger;

    public DatabaseBackedSecurityVersionResolver(
        ICache<Harmony.Core.Cache.Providers.Redis> redis,
        IServiceScopeFactory scopes,
        IMultiTenancyService tenants,
        ILogger<DatabaseBackedSecurityVersionResolver> logger)
    {
        this.redis = redis;
        this.scopes = scopes;
        this.tenants = tenants;
        this.logger = logger;
    }

    public async Task<int?> GetVersionAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var key = SecurityVersionCacheKeys.UserVersionKey(tenantId, userId);

        var (readable, cached) = await this.ReadAsync(key, cancellationToken);
        if (!readable)
        {
            return null;
        }

        if (cached is not null)
        {
            return cached;
        }

        // A tenant this service does not host has no database to ask; its token is refused, never a 500.
        if (!this.tenants.GetTenants().Any(tenant => string.Equals(tenant.Id, tenantId.ToString("D"), StringComparison.OrdinalIgnoreCase)))
        {
            this.logger.LogWarning("Token check: tenant {Tenant} is not configured on this service.", tenantId);
            return null;
        }

        using var scope = this.scopes.CreateScope();
        var services = scope.ServiceProvider;

        if (await services.GetRequiredService<ICacheSeeder>().HealIfLostAsync(cancellationToken))
        {
            (_, cached) = await this.ReadAsync(key, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }
        }

        // PropX: the miss is answered from the database and the key is rewritten.
        var version = await services.GetRequiredService<IdentityDbContext>().UserAccounts
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .Where(u => u.Id == userId && u.TenantId == tenantId)
            .Select(u => (int?)u.SecurityVersion)
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null)
        {
            this.logger.LogWarning("Token check: user {UserId} does not exist in tenant {Tenant}.", userId, tenantId);
            return null;
        }

        await services.GetRequiredService<ICacheService>().AddAsync(key, version.Value, SecurityVersionCacheKeys.UserVersionLifetime, cancellationToken);
        this.logger.LogInformation("Token check healed the cache for user {UserId} in tenant {Tenant} from the database.", userId, tenantId);

        return version;
    }

    // readable = false means Redis itself could not be read: fail closed, do not touch the database.
    private async Task<(bool Readable, int? Value)> ReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await this.redis.GetAsync<int?>(key, eCacheAccessLevel.Public, cancellationToken);

            return response.CacheOperationResult switch
            {
                eCacheOperationResult.Success when response.Value is >= 0 => (true, response.Value),
                eCacheOperationResult.KeyDoesNotExist => (true, null),
                _ => this.Refused(key, response.CacheOperationResult.ToString()),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this.logger.LogError(exception, "Token check refused: Redis could not be read for {Key}.", key);
            return (false, null);
        }
    }

    private (bool, int?) Refused(string key, string result)
    {
        this.logger.LogError("Token check refused: reading {Key} from Redis returned {Result}.", key, result);
        return (false, null);
    }
}
