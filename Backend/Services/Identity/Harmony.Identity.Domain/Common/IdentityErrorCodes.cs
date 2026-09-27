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
}