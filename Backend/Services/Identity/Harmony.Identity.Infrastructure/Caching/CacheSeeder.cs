using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Cache.Enums;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Caching;

/// <summary>
/// PropX's CacheSeeder: at every start every role's permissions and every user's security version are published
/// again, overwriting what is there, so a fresh Redis, a lost key or a publish that failed while Identity was down are
/// all healed before the first request. A sentinel key is written last; when a later request finds it gone, Redis was
/// wiped and <see cref="HealIfLostAsync"/> republishes everything, so the other services heal through Identity.
/// Unlike PropX there is no ClearAllAsync first: Redis is shared and Identity's user may only write Global:identity:*.
/// </summary>
public sealed class CacheSeeder : ICacheSeeder
{
    /// <summary>Global:identity:published = when the last full publish finished. Gone means Redis lost its data.</summary>
    public const string PublishedKey = "identity:published";

    private readonly IdentityDbContext dbContext;
    private readonly ICacheService cacheService;
    private readonly ICache<Harmony.Core.Cache.Providers.Redis> redis;
    private readonly ILogger<CacheSeeder> logger;

    public CacheSeeder(IdentityDbContext dbContext, ICacheService cacheService, ICache<Harmony.Core.Cache.Providers.Redis> redis, ILogger<CacheSeeder> logger)
    {
        this.dbContext = dbContext;
        this.cacheService = cacheService;
        this.redis = redis;
        this.logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await this.SeedRolesPermissions(cancellationToken);
        await this.SeedUserSecurityVersions(cancellationToken);

        await this.cacheService.AddAsync(PublishedKey, DateTimeOffset.UtcNow.ToString("O"), PermissionCacheKeys.RolePermissionsLifetime, cancellationToken);
    }

    public async Task<bool> HealIfLostAsync(CancellationToken cancellationToken = default)
    {
        eCacheOperationResult sentinel;
        try
        {
            sentinel = await this.redis.ExistsAsync(PublishedKey, eCacheAccessLevel.Public, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this.logger.LogError(exception, "Cache heal skipped: Redis could not be read.");
            return false;
        }

        if (sentinel != eCacheOperationResult.KeyDoesNotExist)
        {
            return false;
        }

        this.logger.LogWarning("Redis lost the published keys (sentinel {Key} is gone). Republishing every role and user.", PublishedKey);
        await this.InitializeAsync(cancellationToken);
        return true;
    }

    private async Task SeedRolesPermissions(CancellationToken cancellationToken)
    {
        // The tenant filter is dropped by name so every tenant's roles load in one pass at startup (PropX IgnoreQueryFilters()).
        var roles = await this.dbContext.Roles
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .Include(r => r.Permissions).ThenInclude(rp => rp.Permission)
            .ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            // The super role publishes as every enum value; every other role as its active rows.
            var values = role.EffectivePermissions().Select(v => (int)v).ToList();

            await this.cacheService.AddAsync(
                PermissionCacheKeys.RolePermissionKey(role.TenantId, role.Id),
                values,
                PermissionCacheKeys.RolePermissionsLifetime,
                cancellationToken);
        }

        this.logger.LogInformation(
            "Role permissions published for {Roles} roles across {Tenants} tenants.",
            roles.Count,
            roles.Select(r => r.TenantId).Distinct().Count());
    }

    private async Task SeedUserSecurityVersions(CancellationToken cancellationToken)
    {
        var users = await this.dbContext.UserAccounts
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsNoTracking()
            .Select(u => new { u.Id, u.TenantId, u.SecurityVersion })
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            await this.cacheService.AddAsync(
                SecurityVersionCacheKeys.UserVersionKey(user.TenantId, user.Id),
                user.SecurityVersion,
                SecurityVersionCacheKeys.UserVersionLifetime,
                cancellationToken);
        }

        this.logger.LogInformation("Security versions published for {Users} users.", users.Count);
    }
}
