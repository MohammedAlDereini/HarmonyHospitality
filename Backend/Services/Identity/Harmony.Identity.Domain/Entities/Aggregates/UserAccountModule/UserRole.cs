using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>One row = this account holds this role. Owned by the User aggregate; the token lists the role ids.</summary>
public sealed class UserRole : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    private UserRole()
    {
    }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public Guid RoleId { get; private set; }

    public Role Role { get; private set; } = null!;

    public Guid TenantId { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public static UserRole New(Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("A role id is required.", nameof(roleId));
        }

        return new UserRole { RoleId = roleId };
    }
}