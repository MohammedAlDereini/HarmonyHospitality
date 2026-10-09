namespace Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

/// <summary>
/// A standing as one number, computed in memory: the level is stored as text, so SQL must never order by it.
/// The super role stands above every level; holding no roles at all stands below Standard.
/// </summary>
public static class RoleRank
{
    public const int None = 0;

    public const int Administrator = (int)PrivilegeLevel.Administrator;

    public const int Super = Administrator + 1;

    public static int Of(PrivilegeLevel privilegeLevel, bool isSuperRole)
        => isSuperRole ? Super : (int)privilegeLevel;
}
