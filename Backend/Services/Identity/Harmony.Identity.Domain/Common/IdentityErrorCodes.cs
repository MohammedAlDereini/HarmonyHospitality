namespace Harmony.Identity.Domain.Common;

public static class IdentityErrorCodes
{
    public const string InvalidRoleCode = "00006";
    public const string RoleNameRequired = "00007";
    // 00008, 00010, 00011 were the string permission codes; kept free.
    /// <summary>A permission id that is not in this tenant's catalogue or is inactive.</summary>
    public const string UnknownPermission = "00009";
    public const string SystemRoleImmutable = "00012";
    public const string SystemRoleNotDeletable = "00013";
    public const string InvalidPrivilegeLevel = "00014";
    public const string RoleNotFound = "00015";
    public const string RoleCodeInUse = "00016";
    public const string ReservedRoleCode = "00017";


    // 00018–00035 belong to the Organisation ticket; 00070–00073 to the Tenant ticket.
    // 00037, 00039, 00040, 00043 were service principals; kept free.
    public const string UserAccountNotFound = "00036";
    public const string DisplayNameRequired = "00038";
    public const string UserAccountTerminated = "00041";
    public const string IdentityStoreRefused = "00042";
}