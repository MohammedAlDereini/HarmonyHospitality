using Harmony.Identity.Domain.Common;

namespace Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

public static class SystemRoles
{
    public const string SuperAdmin = "SUPERADMIN";

    /// <summary>The role every customer tenant is born with: level Administrator, every tenant permission, never edited or deleted.</summary>
    public const string Administrator = "ADMINISTRATOR";

    public static bool IsReserved(string? code)
        => IdentityText.SameCode(code, SuperAdmin) || IdentityText.SameCode(code, Administrator);
}