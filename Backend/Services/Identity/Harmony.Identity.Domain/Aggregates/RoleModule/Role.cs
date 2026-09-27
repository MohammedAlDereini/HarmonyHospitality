using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Core.Exceptions;
using Harmony.Core.Models;
using Harmony.Identity.Domain.Common;

namespace Harmony.Identity.Domain.Aggregates.RoleModule;

public sealed class Role : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    private readonly List<string> _permissionCodes = [];
    private Role()
    {
    }

    public string Code { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string? NameAr { get; private set; }

    public PrivilegeLevel PrivilegeLevel { get; private set; }

    public bool IsSystemRole { get; private set; }

    public bool IsSuperRole { get; private set; }

    public IReadOnlyCollection<string> PermissionCodes => IsSuperRole ? PermissionCatalog.All : _permissionCodes.AsReadOnly();

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

    public static Role CreateSystem(string code, string nameEn, string? nameAr, PrivilegeLevel privilegeLevel, IEnumerable<string> permissionCodes)
    {
        ArgumentNullException.ThrowIfNull(permissionCodes);

        var role = new Role
        {
            Code = AssignableCode(code),
            NameEn = RequireName(nameEn),
            NameAr = IdentityText.Blank(nameAr),
            PrivilegeLevel = RequireLevel(privilegeLevel),
            IsSystemRole = true,
        };

        foreach (var permissionCode in permissionCodes)
        {
            role.AddPermission(permissionCode);
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

    public bool Has(string permissionCode)
        => IsSuperRole
            ? PermissionCatalog.TryGetCanonical(permissionCode, out _)
            : _permissionCodes.Any(c => IdentityText.SameCode(c, permissionCode));

    public void Grant(string permissionCode)
    {
        AssertEditable();
        AddPermission(permissionCode);
    }

    public void Revoke(string permissionCode)
    {
        AssertEditable();

        var existing = _permissionCodes.FirstOrDefault(c => IdentityText.SameCode(c, permissionCode))
            ?? throw new BusinessException($"'{Code}' does not hold '{permissionCode}'.", Error.New(IdentityErrorCodes.PermissionNotHeld));

        _permissionCodes.Remove(existing);
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
            throw new BusinessException($"'{Code}' is a system role and cannot be deleted.", Error.New(IdentityErrorCodes.SystemRoleNotDeletable));
        }
    }

    private void AddPermission(string permissionCode)
    {
        var code = IdentityText.PermissionCode(permissionCode);

        if (!PermissionCatalog.TryGetCanonical(code, out var canonical))
        {
            throw new BusinessException($"'{code}' is not in the permission catalogue.", Error.New(IdentityErrorCodes.UnknownPermission));
        }

        if (Has(canonical))
        {
            throw new BusinessException($"'{Code}' already holds '{canonical}'.", Error.New(IdentityErrorCodes.PermissionAlreadyGranted));
        }

        _permissionCodes.Add(canonical);
    }

    private void AssertEditable()
    {
        if (IsSystemRole)
        {
            throw new BusinessException($"'{Code}' is a system role. Create your own role instead of changing a shipped one.", Error.New(IdentityErrorCodes.SystemRoleImmutable));
        }
    }

    private static string AssignableCode(string code)
    {
        var normalized = IdentityText.RoleCode(code);

        return SystemRoles.IsReserved(normalized)
            ? throw new BusinessException($"'{normalized}' is reserved for the system.", Error.New(IdentityErrorCodes.ReservedRoleCode))
            : normalized;
    }

    private static string RequireName(string nameEn)
        => IdentityText.Blank(nameEn)
            ?? throw new BusinessException("A role name is required.", Error.New(IdentityErrorCodes.RoleNameRequired));

    private static PrivilegeLevel RequireLevel(PrivilegeLevel privilegeLevel)
        => Enum.IsDefined(privilegeLevel)
            ? privilegeLevel
            : throw new BusinessException($"'{privilegeLevel}' is not a valid privilege level.", Error.New(IdentityErrorCodes.InvalidPrivilegeLevel));
}