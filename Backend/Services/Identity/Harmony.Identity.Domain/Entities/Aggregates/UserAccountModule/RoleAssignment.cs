using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// This account holds this role at one hotel or across one group, from now until EndsAtUtc (null = open). Owned by
/// the account, born and ended only through it. Not in the token: the services resolve it per call for the hotel the
/// call is about. Ending is a date, never a delete: the trail and the invoices of last month still resolve.
/// </summary>
public sealed class RoleAssignment : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    private RoleAssignment()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public AssignmentScope Scope { get; private set; } = null!;

    /// <summary>Null = open-ended.</summary>
    public DateTimeOffset? EndsAtUtc { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public bool IsCurrentAt(DateTimeOffset utcNow) => EndsAtUtc is null || EndsAtUtc > utcNow;

    internal static RoleAssignment New(Guid tenantId, Guid roleId, AssignmentScope scope, DateTimeOffset? endsAtUtc)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("A role is required.", nameof(roleId));
        }

        return new RoleAssignment { TenantId = tenantId, RoleId = roleId, Scope = scope, EndsAtUtc = endsAtUtc?.ToUniversalTime() };
    }

    internal void End(DateTimeOffset atUtc)
    {
        if (!IsCurrentAt(atUtc))
        {
            throw new InvalidOperationException("The assignment has already ended.");
        }

        EndsAtUtc = atUtc.ToUniversalTime();
    }
}
