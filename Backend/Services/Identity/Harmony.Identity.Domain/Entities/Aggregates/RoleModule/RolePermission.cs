using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;

namespace Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

/// <summary>One row = this role holds this permission. Owned by the Role aggregate (PropX RolePermission).</summary>
public sealed class RolePermission : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    private RolePermission()
    {
    }

    public Guid RoleId { get; private set; }

    public Role Role { get; private set; } = null!;

    public Guid PermissionId { get; private set; }

    public Permission Permission { get; private set; } = null!;

    public Guid TenantId { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    /// <summary>RoleId is set by the role that adds the row to its collection.</summary>
    public static RolePermission New(Guid permissionId)
    {
        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("A permission id is required.", nameof(permissionId));
        }

        return new RolePermission { PermissionId = permissionId };
    }
}
