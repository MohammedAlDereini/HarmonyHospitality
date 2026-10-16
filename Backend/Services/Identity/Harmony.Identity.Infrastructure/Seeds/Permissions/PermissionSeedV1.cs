using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
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
/// row's scope in step with the catalogue; nothing else existing is touched. Version 2: the scope.
/// </summary>
internal sealed class PermissionSeedV1 : IDbSeed<IdentityDbContext>
{
    public int Version => 2;

    /// <summary>Name, description and endpoints per value. Documentation for the table; enforcement is the attribute.</summary>
    internal static readonly IReadOnlyList<Permission> Catalogue =
    [
        // Lookups
        Permission.New(PermissionEnum.ViewLookups,           "View Lookups",             "View lookup categories and values.",                       "POST api/LookupCategory/get-all; POST api/LookupValue/get-all"),
        Permission.New(PermissionEnum.ManageLookups,         "Manage Lookups",           "Create, edit and delete lookup categories and values.",    "POST/PUT/DELETE api/LookupCategory, api/LookupValue"),
        // Roles & permissions
        Permission.New(PermissionEnum.ViewRoles,             "View Roles",               "View the roles list and open a role.",                     "POST api/Role/get-all; GET api/Role/{id}; GET api/Role/permissions"),
        Permission.New(PermissionEnum.CreateRole,            "Create Role",              "Create a role.",                                           "POST api/Role/Create"),
        Permission.New(PermissionEnum.EditRole,              "Edit Role",                "Rename a role, change its code or privilege level.",       "PUT api/Role/Update"),
        Permission.New(PermissionEnum.DeleteRole,            "Delete Role",              "Delete a role nobody holds.",                              "DELETE api/Role/{id}"),
        Permission.New(PermissionEnum.ManageRolePermissions, "Manage Role Permissions",  "Grant and revoke permissions on a role.",                  "POST api/Role/GrantPermission; POST api/Role/RevokePermission"),
        Permission.New(PermissionEnum.AssignUsersToRole,     "Assign Users to Role",     "Set the roles an account holds.",                          "PUT api/UserAccount/UpdateRoles"),
        // Users
        Permission.New(PermissionEnum.ViewUsers,             "View Users",               "View the accounts list and open an account.",              "POST api/UserAccount/get-all; GET api/UserAccount/{id}"),
        Permission.New(PermissionEnum.ManageUsers,           "Manage Users",             "Create accounts, suspend, reinstate, terminate.",          "PUT api/UserAccount/Suspend; PUT api/UserAccount/Reinstate; PUT api/UserAccount/Terminate"),
        // Platform
        Permission.New(PermissionEnum.ViewTenants,           "View Tenants",             "View tenants and shared (global) data.",                   "POST api/Tenant/get-all; GET api/Tenant/{id}; the reads of the reference tables", scope: PermissionScope.Platform),
        Permission.New(PermissionEnum.ManageTenants,         "Manage Tenants",           "Manage tenants and shared (global) data.",                 "POST api/Tenant/Create; PUT api/Tenant/Update, Suspend, Reinstate; the writes of the reference tables", scope: PermissionScope.Platform),
    ];

    /// <summary>The rows a tenant is for: everything for the platform tenant, the tenant-scope rows for a customer.</summary>
    internal static IEnumerable<Permission> CatalogueFor(bool isPlatformTenant)
        => Catalogue.Where(p => isPlatformTenant || p.Scope == PermissionScope.Tenant);

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
            var scope = Catalogue.First(p => p.PermissionValue == row.PermissionValue).Scope;
            if (row.Scope != scope)
            {
                row.SetScope(scope);
                changed++;
            }
        }

        var missing = CatalogueFor(isPlatform).Where(p => existing.All(e => e.PermissionValue != p.PermissionValue)).ToList();

        // Fresh instances per tenant: an entity may belong to one context only.
        await dbContext.Permissions.AddRangeAsync(
            missing.Select(p => Permission.New(p.PermissionValue, p.Name, p.Description, p.EndPoint, p.IsActive, p.Scope)),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        serviceProvider.GetRequiredService<ILogger<PermissionSeedV1>>()
            .LogInformation("Permissions seeded for tenant {Tenant}: {Count} added, {Changed} rescoped, {Total} in the catalogue (platform: {IsPlatform}).", tenant.Id, missing.Count, changed, Catalogue.Count, isPlatform);
    }
}
