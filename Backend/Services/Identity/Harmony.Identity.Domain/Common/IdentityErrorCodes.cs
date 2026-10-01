namespace Harmony.Identity.Domain.Common;

public static class IdentityErrorCodes
{
    public const string InvalidRoleCode = "00006";
    public const string RoleNameRequired = "00007";
    public const string InvalidPermissionCode = "00008";
    public const string UnknownPermission = "00009";
    public const string PermissionAlreadyGranted = "00010";
    public const string PermissionNotHeld = "00011";
    public const string SystemRoleImmutable = "00012";
    public const string SystemRoleNotDeletable = "00013";
    public const string InvalidPrivilegeLevel = "00014";
    public const string RoleNotFound = "00015";
    public const string RoleCodeInUse = "00016";
    public const string ReservedRoleCode = "00017";


    // 00018–00035 belong to the Organisation ticket; 00070–00073 to the Tenant ticket.
    public const string UserAccountNotFound = "00036";
    public const string InvalidServicePrincipalCode = "00037";
    public const string DisplayNameRequired = "00038";
    public const string ServicePrincipalCodeInUse = "00039";
    public const string NotAServicePrincipal = "00040";
    public const string UserAccountTerminated = "00041";
    public const string IdentityStoreRefused = "00042";
}