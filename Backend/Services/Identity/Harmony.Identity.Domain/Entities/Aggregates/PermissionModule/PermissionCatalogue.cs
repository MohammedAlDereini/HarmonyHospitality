namespace Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;

using Harmony.Core.Identity.Permissions;

/// <summary>One line of the catalogue: what a permission is called, what it is for, and which scope it belongs to.</summary>
public sealed record PermissionCatalogueEntry(PermissionEnum Value, string Name, string Description, string EndPoint, PermissionScope Scope = PermissionScope.Tenant);

/// <summary>
/// The permission catalogue, the business truth every tenant's rows are made from: the platform tenant gets every line,
/// a customer gets the tenant-scope lines only (the platform-only permissions never exist inside a customer, 00073). The
/// seed reads it at startup for the tenants that already exist; the birth of a tenant reads it for the new one. Names,
/// descriptions and endpoints are documentation for the table; enforcement is the attribute on each door.
/// </summary>
public static class PermissionCatalogue
{
    public static readonly IReadOnlyList<PermissionCatalogueEntry> All =
    [
        // Lookups
        new(PermissionEnum.ViewLookups,           "View Lookups",             "View lookup categories and values.",                       "POST api/LookupCategory/get-all; POST api/LookupValue/get-all"),
        new(PermissionEnum.ManageLookups,         "Manage Lookups",           "Create, edit and delete lookup categories and values.",    "POST/PUT/DELETE api/LookupCategory, api/LookupValue"),
        // Roles & permissions
        new(PermissionEnum.ViewRoles,             "View Roles",               "View the roles list and open a role.",                     "POST api/Role/get-all; GET api/Role/{id}; GET api/Role/permissions"),
        new(PermissionEnum.CreateRole,            "Create Role",              "Create a role.",                                           "POST api/Role/Create"),
        new(PermissionEnum.EditRole,              "Edit Role",                "Rename a role, change its code or privilege level.",       "PUT api/Role/Update"),
        new(PermissionEnum.DeleteRole,            "Delete Role",              "Delete a role nobody holds.",                              "DELETE api/Role/{id}"),
        new(PermissionEnum.ManageRolePermissions, "Manage Role Permissions",  "Grant and revoke permissions on a role.",                  "POST api/Role/GrantPermission; POST api/Role/RevokePermission"),
        new(PermissionEnum.AssignUsersToRole,     "Assign Users to Role",     "Set the roles an account holds.",                          "PUT api/UserAccount/UpdateRoles"),
        // Users
        new(PermissionEnum.ViewUsers,             "View Users",               "View the accounts list and open an account.",              "POST api/UserAccount/get-all; GET api/UserAccount/{id}"),
        new(PermissionEnum.ManageUsers,           "Manage Users",             "Create accounts, suspend, reinstate, terminate.",          "PUT api/UserAccount/Suspend; PUT api/UserAccount/Reinstate; PUT api/UserAccount/Terminate"),
        // Platform
        new(PermissionEnum.ViewTenants,           "View Tenants",             "View tenants and shared (global) data.",                   "POST api/Tenant/get-all; GET api/Tenant/{id}; the reads of the reference tables", PermissionScope.Platform),
        new(PermissionEnum.ManageTenants,         "Manage Tenants",           "Manage tenants and shared (global) data.",                 "POST api/Tenant/Create; PUT api/Tenant/Update, Suspend, Reinstate; the writes of the reference tables", PermissionScope.Platform),
        // Properties
        new(PermissionEnum.ViewProperties,        "View Properties",          "View the customer's own hotels and the lines they hold.",  "POST api/Property/mine; GET api/Property/mine/{id}; GET api/Tenant/mine"),
        new(PermissionEnum.ManageProperties,      "Manage Properties",        "Edit a hotel's own details and the people named at it.",   "PUT api/Property/Update; the contact doors"),
        // Settings
        new(PermissionEnum.ViewSettings,          "View Settings",            "See the customer's setting values and why a hotel's value wins.", "POST api/ConfigValue/mine; POST api/ConfigSetting/get-all; GET api/ConfigSetting/{key}"),
        new(PermissionEnum.ManageSettings,        "Manage Settings",          "Set and clear setting values for the customer, a group or a hotel.", "PUT api/ConfigValue/Set; PUT api/ConfigValue/Clear"),
    ];

    /// <summary>The lines a tenant is for: everything for the platform tenant, the tenant-scope lines for a customer.</summary>
    public static IEnumerable<PermissionCatalogueEntry> For(bool platformTenant)
        => All.Where(entry => platformTenant || entry.Scope == PermissionScope.Tenant);

    /// <summary>A fresh row from a line: an entity belongs to one context, so every tenant gets its own instances.</summary>
    public static Permission ToPermission(this PermissionCatalogueEntry entry)
        => Permission.New(entry.Value, entry.Name, entry.Description, entry.EndPoint, isActive: true, entry.Scope);
}
