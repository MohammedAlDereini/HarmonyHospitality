using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Core.Identity.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Caching;

/// <summary>
/// PropX's CacheSeeder: at every start every role of every tenant is published again, overwriting what is there, so a
/// fresh Redis, a lost key or a publish that failed while Identity was down are all healed before the first request.
/// Unlike PropX there is no ClearAllAsync first: Redis is shared and Identity's user may only write Global:identity:*.
/// </summary>
public sealed class CacheSeeder : ICacheSeeder
{
    private readonly IdentityDbContext dbContext;
    private readonly ICacheService cacheService;
    private readonly ILogger<CacheSeeder> logger;

    public CacheSeeder(IdentityDbContext dbContext, ICacheService cacheService, ILogger<CacheSeeder> logger)
    {
        this.dbContext = dbContext;
        this.cacheService = cacheService;
        this.logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await this.SeedRolesPermissions(cancellationToken);
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
}
