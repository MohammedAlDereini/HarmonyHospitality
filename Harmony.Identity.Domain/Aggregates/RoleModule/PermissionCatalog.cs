namespace Harmony.Identity.Domain.Aggregates.RoleModule;

public static class PermissionCatalog
{
    public const string PlatformViewTenant = "Platform.ViewTenant";
    public const string PlatformManageTenant = "Platform.ManageTenant";
    public const string LookupView = "Lookup.View";
    public const string LookupManage = "Lookup.Manage";
    public const string IdentityViewRole = "Identity.ViewRole";
    public const string IdentityManageRole = "Identity.ManageRole";
    public const string IdentityGrantPermission = "Identity.GrantPermission";
    public const string IdentityRevokePermission = "Identity.RevokePermission";
    public const string IdentityDeleteRole = "Identity.DeleteRole";
    public const string IdentityViewUser = "Identity.ViewUser";
    public const string IdentityManageUser = "Identity.ManageUser";
    public const string IdentityAssignRole = "Identity.AssignRole";

    private static readonly Dictionary<string, string> Known = new[]
    {
        LookupView,
        LookupManage,
        IdentityViewRole,
        IdentityManageRole,
        IdentityGrantPermission,
        IdentityRevokePermission,
        IdentityDeleteRole,
        IdentityViewUser,
        IdentityManageUser,
        IdentityAssignRole,
        PlatformViewTenant,
        PlatformManageTenant,
    }.ToDictionary(code => code, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> All => Known.Values;

    public static bool TryGetCanonical(string? code, out string canonical)
    {
        canonical = string.Empty;

        if (code is null || !Known.TryGetValue(code.Trim(), out var found))
        {
            return false;
        }

        canonical = found;
        return true;
    }
}