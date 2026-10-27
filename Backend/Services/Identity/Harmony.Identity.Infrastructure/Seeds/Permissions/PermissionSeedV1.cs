using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Core.Identity.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Seeds.Permissions;

/// <summary>
/// The permission catalogue as rows, per tenant: one per <see cref="PermissionEnum"/> value the tenant is for. Every
/// tenant gets the tenant-scope rows; the platform-only rows (ViewTenants, ManageTenants) go to the platform tenant
/// alone, so no customer role can ever hold them. Inserts only the values the tenant does not have yet and keeps each
/// row's scope in step with the catalogue; nothing else existing is touched. Version 2: the scope. Version 3: the two
/// settings permissions (ViewSettings, ManageSettings). A new catalogue line needs a new version, or the configured
/// tenants never get its row and PermissionEnumIntegrityCheck refuses to start. The catalogue itself
/// lives in the domain (<see cref="PermissionCatalogue"/>): the birth of a tenant reads the same list.
/// </summary>
internal sealed class PermissionSeedV1 : IDbSeed<IdentityDbContext>
{
    public int Version => 3;

    public async Task SeedAsync(IdentityDbContext dbContext, Tenant tenant, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var tenantId = new Guid(tenant.Id);
        var platformTenantId = serviceProvider.GetRequiredService<IConfiguration>()["ApplicationSettings:ApplicationKeys:PlatformTenantId"]
            ?? throw new InvalidOperationException("ApplicationSettings:ApplicationKeys:PlatformTenantId is not configured.");
        var isPlatform = string.Equals(tenant.Id, platformTenantId, StringComparison.OrdinalIgnoreCase);

        var existing = await dbContext.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        // A row's scope follows the catalogue (the platform tenant's rows were written before scopes existed).
        var changed = 0;
        foreach (var row in existing)
        {
            var scope = PermissionCatalogue.All.First(p => p.Value == row.PermissionValue).Scope;
            if (row.Scope != scope)
            {
                row.SetScope(scope);
                changed++;
            }
        }

        var missing = PermissionCatalogue.For(isPlatform).Where(p => existing.All(e => e.PermissionValue != p.Value)).ToList();

        // Fresh instances per tenant: an entity may belong to one context only.
        await dbContext.Permissions.AddRangeAsync(missing.Select(p => p.ToPermission()), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        serviceProvider.GetRequiredService<ILogger<PermissionSeedV1>>()
            .LogInformation("Permissions seeded for tenant {Tenant}: {Count} added, {Changed} rescoped, {Total} in the catalogue (platform: {IsPlatform}).", tenant.Id, missing.Count, changed, PermissionCatalogue.All.Count, isPlatform);
    }
}
