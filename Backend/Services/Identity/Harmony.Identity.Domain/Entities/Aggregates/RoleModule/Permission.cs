using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Shared.Enums;

namespace Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

/// <summary>A permission as a row: one per <see cref="PermissionEnum"/> value per tenant, seeded, never created through the API.</summary>
public sealed class Permission : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    private Permission()
    {
    }

    /// <summary>The enum value, stored as int. The code's identity.</summary>
    public PermissionEnum PermissionValue { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    /// <summary>The endpoints this permission opens, for people reading the table. Documentation, not enforcement.</summary>
    public string? EndPoint { get; private set; }

    public bool IsActive { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public static Permission New(PermissionEnum permissionValue, string name, string? description = null, string? endPoint = null, bool isActive = true)
    {
        if (!Enum.IsDefined(permissionValue))
        {
            throw new ArgumentOutOfRangeException(nameof(permissionValue), permissionValue, "Not a permission in the catalogue.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A permission name is required.", nameof(name));
        }

        return new Permission
        {
            PermissionValue = permissionValue,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            EndPoint = string.IsNullOrWhiteSpace(endPoint) ? null : endPoint.Trim(),
            IsActive = isActive,
        };
    }

    public void Update(string name, string? description, string? endPoint)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A permission name is required.", nameof(name));
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        EndPoint = string.IsNullOrWhiteSpace(endPoint) ? null : endPoint.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
