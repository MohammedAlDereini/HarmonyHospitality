namespace Harmony.Identity.Shared.Enums;

/// <summary>
/// The permission catalogue. One row per value is seeded in every tenant's Permissions table; roles hold rows;
/// [RequirePermission] names values. Numbers are the stored value and never change or get reused.
/// </summary>
public enum PermissionEnum
{
    // ── Lookups ───────────────────────────────────────────────────────────────
    /// <summary>View lookup categories and values.</summary>
    ViewLookups = 1,
    /// <summary>Create, edit, delete lookup categories and values.</summary>
    ManageLookups = 2,

    // ── Roles & permissions ───────────────────────────────────────────────────
    /// <summary>View the roles list and open a role.</summary>
    ViewRoles = 3,
    /// <summary>Create a role.</summary>
    CreateRole = 4,
    /// <summary>Rename a role, change its code or privilege level.</summary>
    EditRole = 5,
    /// <summary>Delete a role.</summary>
    DeleteRole = 6,
    /// <summary>Grant and revoke permissions on a role.</summary>
    ManageRolePermissions = 7,
    /// <summary>Set the roles an account holds.</summary>
    AssignUsersToRole = 8,

    // ── Users ─────────────────────────────────────────────────────────────────
    /// <summary>View the accounts list and open an account.</summary>
    ViewUsers = 9,
    /// <summary>Create accounts, suspend, reinstate, terminate.</summary>
    ManageUsers = 10,

    // ── Platform ──────────────────────────────────────────────────────────────
    /// <summary>View tenants and global (shared) data.</summary>
    ViewTenants = 11,
    /// <summary>Manage tenants and global (shared) data.</summary>
    ManageTenants = 12,
}
