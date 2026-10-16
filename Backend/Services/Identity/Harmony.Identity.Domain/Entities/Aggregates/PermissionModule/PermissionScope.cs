namespace Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;

/// <summary>
/// Who a permission is for. Tenant: a customer's own work (roles, people, later properties); every tenant gets these
/// rows. Platform: running the customers and the shared reference data (ViewTenants, ManageTenants); only the
/// platform tenant gets these rows, so no customer role can ever hold them.
/// </summary>
public enum PermissionScope
{
    Tenant = 1,
    Platform = 2,
}
