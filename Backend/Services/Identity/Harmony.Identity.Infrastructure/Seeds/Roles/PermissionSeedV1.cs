using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Identity.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Seeds.Roles;

/// <summary>
/// The permission catalogue as rows, per tenant: one per <see cref="PermissionEnum"/> value. Runs at every start
/// and inserts only the values the tenant does not have yet, so a new enum member reaches every tenant on the next
/// deployment and nothing existing is touched.
/// </summary>
internal sealed class PermissionSeedV1 : IDbSeed<IdentityDbContext>
{
    public int Version => 1;

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
        Permission.New(PermissionEnum.ViewTenants,           "View Tenants",             "View tenants and shared (global) data.",                   null),
        Permission.New(PermissionEnum.ManageTenants,         "Manage Tenants",           "Manage tenants and shared (global) data.",                 null),
    ];

    public async Task SeedAsync(IdentityDbContext dbContext, Tenant tenant, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var tenantId = new Guid(tenant.Id);

        var existing = await dbContext.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.PermissionValue)
            .ToListAsync(cancellationToken);

        var missing = Catalogue.Where(p => !existing.Contains(p.PermissionValue)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        // Fresh instances per tenant: an entity may belong to one context only.
        await dbContext.Permissions.AddRangeAsync(
            missing.Select(p => Permission.New(p.PermissionValue, p.Name, p.Description, p.EndPoint, p.IsActive)),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        serviceProvider.GetRequiredService<ILogger<PermissionSeedV1>>()
            .LogInformation("Permissions seeded for tenant {Tenant}: {Count} added, {Total} in the catalogue.", tenant.Id, missing.Count, Catalogue.Count);
    }
}
