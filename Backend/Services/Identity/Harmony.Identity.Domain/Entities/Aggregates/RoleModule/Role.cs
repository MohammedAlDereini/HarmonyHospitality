using Harmony.Core.Errors;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Core.Exceptions;
using Harmony.Core.Models;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Shared.Enums;

namespace Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

public sealed class Role : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    private Role()
    {
    }

    public string Code { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string? NameAr { get; private set; }

    public PrivilegeLevel PrivilegeLevel { get; private set; }

    public bool IsSystemRole { get; private set; }

    public bool IsSuperRole { get; private set; }

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    /// <summary>The permissions this role holds, as rows (PropX RolePermissions). The super role holds none: it passes every check.</summary>
    public ICollection<RolePermission> Permissions { get; private set; } = new List<RolePermission>();

    public bool IsPrivileged => PrivilegeLevel >= PrivilegeLevel.Manager;

    public Guid TenantId { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public static Role Create(string code, string nameEn, string? nameAr = null, PrivilegeLevel privilegeLevel = PrivilegeLevel.Standard)
    {
        return new Role
        {
            Code = AssignableCode(code),
            NameEn = RequireName(nameEn),
            NameAr = IdentityText.Blank(nameAr),
            PrivilegeLevel = RequireLevel(privilegeLevel),
            IsSystemRole = false,
        };
    }

    public static Role CreateSystem(string code, string nameEn, string? nameAr, PrivilegeLevel privilegeLevel, IEnumerable<Guid> permissionIds)
    {
        ArgumentNullException.ThrowIfNull(permissionIds);

        var role = new Role
        {
            Code = AssignableCode(code),
            NameEn = RequireName(nameEn),
            NameAr = IdentityText.Blank(nameAr),
            PrivilegeLevel = RequireLevel(privilegeLevel),
            IsSystemRole = true,
        };

        foreach (var permissionId in permissionIds.Distinct())
        {
            role.Permissions.Add(RolePermission.New(permissionId));
        }

        return role;
    }

    public static Role CreateSuper(string code, string nameEn, string? nameAr)
    {
        return new Role
        {
            Code = IdentityText.RoleCode(code),
            NameEn = RequireName(nameEn),
            NameAr = IdentityText.Blank(nameAr),
            PrivilegeLevel = PrivilegeLevel.Administrator,
            IsSystemRole = true,
            IsSuperRole = true,
        };
    }

    public bool Has(Guid permissionId)
        => IsSuperRole || Permissions.Any(rp => rp.PermissionId == permissionId);

    /// <summary>
    /// What the role may do, as published to the cache: every value of the enum for the super role, otherwise the
    /// values of its active permission rows. Needs the Permission navigation loaded.
    /// </summary>
    public IReadOnlyList<PermissionEnum> EffectivePermissions()
        => IsSuperRole
            ? Enum.GetValues<PermissionEnum>().Order().ToList()
            : Permissions
                .Where(rp => rp.Permission is { IsActive: true })
                .Select(rp => rp.Permission.PermissionValue)
                .Distinct()
                .Order()
                .ToList();

    /// <summary>The role holds exactly these permissions afterwards: missing ones are added, others removed. System roles refuse.</summary>
    public void UpdatePermissions(IEnumerable<Guid> permissionIds)
    {
        ArgumentNullException.ThrowIfNull(permissionIds);
        AssertEditable();

        var wanted = permissionIds.Distinct().ToList();
        var current = Permissions.Select(rp => rp.PermissionId).ToList();

        foreach (var permissionId in wanted.Except(current))
        {
            Permissions.Add(RolePermission.New(permissionId));
        }

        foreach (var rolePermission in Permissions.Where(rp => !wanted.Contains(rp.PermissionId)).ToList())
        {
            Permissions.Remove(rolePermission);
        }
    }

    public void Rename(string nameEn, string? nameAr)
    {
        NameEn = RequireName(nameEn);
        NameAr = IdentityText.Blank(nameAr);
    }

    public void ChangeCode(string code)
    {
        AssertEditable();
        Code = AssignableCode(code);
    }

    public void SetPrivilegeLevel(PrivilegeLevel privilegeLevel)
    {
        AssertEditable();
        PrivilegeLevel = RequireLevel(privilegeLevel);
    }

    public void AssertDeletable()
    {
        if (IsSystemRole)
        {
            throw new BusinessException($"'{Code}' is a system role and cannot be deleted.", Error.New(BusinessErrorCodes.Identity.Role.SystemRoleNotDeletable));
        }
    }

    private void AssertEditable()
    {
        if (IsSystemRole)
        {
            throw new BusinessException($"'{Code}' is a system role. Create your own role instead of changing a shipped one.", Error.New(BusinessErrorCodes.Identity.Role.SystemRoleImmutable));
        }
    }

    private static string AssignableCode(string code)
    {
        var normalized = IdentityText.RoleCode(code);

        return SystemRoles.IsReserved(normalized)
            ? throw new BusinessException($"'{normalized}' is reserved for the system.", Error.New(BusinessErrorCodes.Identity.Role.ReservedCode))
            : normalized;
    }

    private static string RequireName(string nameEn)
        => IdentityText.Blank(nameEn)
            ?? throw new BusinessException("A role name is required.", Error.New(BusinessErrorCodes.Identity.Role.NameRequired));

    private static PrivilegeLevel RequireLevel(PrivilegeLevel privilegeLevel)
        => Enum.IsDefined(privilegeLevel)
            ? privilegeLevel
            : throw new BusinessException($"'{privilegeLevel}' is not a valid privilege level.", Error.New(BusinessErrorCodes.Identity.Role.InvalidPrivilegeLevel));
}
