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
/// PropX PermissionAuthorizationService, Identity's side: a role's permissions come from the cache, and a miss is
/// answered from the database and written back, so the next request is a hit. Only Identity can do this; the other
/// services read the same key through the framework and fail closed on a miss. When the miss turns out to be a wiped
/// Redis, everything is republished first, which heals the other services too.
/// The database work runs in its own scope: the request's DbContext captures the tenant when it is constructed, and a
/// permission check may run before the call context has one, so the request's DbContext is never touched here.
/// </summary>
public sealed class DatabaseBackedPermissionResolver : IPlatformPermissionResolver
{
    private readonly ICache<Harmony.Core.Cache.Providers.Redis> redis;
    private readonly IServiceScopeFactory scopes;
    private readonly IMultiTenancyService tenants;
    private readonly ILogger<DatabaseBackedPermissionResolver> logger;

    public DatabaseBackedPermissionResolver(
        ICache<Harmony.Core.Cache.Providers.Redis> redis,
        IServiceScopeFactory scopes,
        IMultiTenancyService tenants,
        ILogger<DatabaseBackedPermissionResolver> logger)
    {
        this.redis = redis;
        this.scopes = scopes;
        this.tenants = tenants;
        this.logger = logger;
    }

    public async Task<IReadOnlySet<int>> GetPermissionsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var key = PermissionCacheKeys.RolePermissionKey(tenantId, roleId);

        var (readable, cached) = await this.ReadAsync(key, cancellationToken);
        if (!readable)
        {
            return new HashSet<int>();
        }

        if (cached is not null)
        {
            return cached.ToHashSet();
        }

        // A tenant this service does not host has no database to ask; its token is refused, never a 500.
        if (!this.tenants.GetTenants().Any(tenant => string.Equals(tenant.Id, tenantId.ToString("D"), StringComparison.OrdinalIgnoreCase)))
        {
            this.logger.LogWarning("Permission check: tenant {Tenant} is not configured on this service.", tenantId);
            return new HashSet<int>();
        }

        using var scope = this.scopes.CreateScope();
        var services = scope.ServiceProvider;

        // A miss: either Redis was wiped (republish everything, then read again) or the role is unknown to the cache.
        if (await services.GetRequiredService<ICacheSeeder>().HealIfLostAsync(cancellationToken))
        {
            (_, cached) = await this.ReadAsync(key, cancellationToken);
            if (cached is not null)
            {
                return cached.ToHashSet();
            }
        }

        // PropX: the miss is answered from the database and the key is rewritten.
        var role = await services.GetRequiredService<IdentityDbContext>().Roles
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .Include(r => r.Permissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role is null)
        {
            this.logger.LogWarning("Permission check: role {RoleId} does not exist in tenant {Tenant}.", roleId, tenantId);
            return new HashSet<int>();
        }

        var values = role.EffectivePermissions().Select(v => (int)v).ToList();
        await services.GetRequiredService<ICacheService>().AddAsync(key, values, PermissionCacheKeys.RolePermissionsLifetime, cancellationToken);
        this.logger.LogInformation("Permission check healed the cache for role {RoleId} in tenant {Tenant} from the database.", roleId, tenantId);

        return values.ToHashSet();
    }

    // readable = false means Redis itself could not be read: fail closed, do not touch the database.
    private async Task<(bool Readable, List<int>? Value)> ReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await this.redis.GetAsync<List<int>>(key, eCacheAccessLevel.Public, cancellationToken);

            return response.CacheOperationResult switch
            {
                eCacheOperationResult.Success when response.Value is not null => (true, response.Value),
                eCacheOperationResult.KeyDoesNotExist => (true, null),
                _ => this.Refused(key, response.CacheOperationResult.ToString()),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this.logger.LogError(exception, "Permission check refused: Redis could not be read for {Key}.", key);
            return (false, null);
        }
    }

    private (bool, List<int>?) Refused(string key, string result)
    {
        this.logger.LogError("Permission check refused: reading {Key} from Redis returned {Result}.", key, result);
        return (false, null);
    }
}