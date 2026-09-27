using Harmony.Identity.Domain.Common;

namespace Harmony.Identity.Domain.Aggregates.RoleModule;

public static class SystemRoles
{
    public const string SuperAdmin = "SUPERADMIN";

    public static bool IsReserved(string? code)
        => IdentityText.SameCode(code, SuperAdmin);
}