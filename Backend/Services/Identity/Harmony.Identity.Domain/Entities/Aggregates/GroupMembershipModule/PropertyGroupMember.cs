using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.DomainEvents.GroupMembershipModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;

namespace Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;

/// <summary>
/// One hotel in one group, for a period (design 1.4, the ERD's PropertyGroupMembers). A relationship with its own
/// lifetime, so its own small root: forty hotels never load one giant group. Inside the wall. The TypeCode copy is
/// what makes "one brand per hotel" a single-table filtered index in SQL; the composite key keeps the copy honest.
/// A membership ends once; a rebrand is a new row.
/// </summary>
public sealed class PropertyGroupMember : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    private PropertyGroupMember()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid PropertyGroupId { get; private set; }

    public Guid PropertyId { get; private set; }

    /// <summary>A copy of the group's type, held honest by the composite key.</summary>
    public string TypeCode { get; private set; } = null!;

    public MembershipPeriod Period { get; private set; } = null!;

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public bool IsCurrent => Period.IsCurrent;

    /// <summary>A hotel joins a group from a moment on. The group gives the tenant and the type; the hotel is the handler's to check.</summary>
    public static PropertyGroupMember Join(PropertyGroup group, Guid propertyId, DateTimeOffset fromUtc)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (propertyId == Guid.Empty)
        {
            throw new ArgumentException("A hotel is required.", nameof(propertyId));
        }

        var member = new PropertyGroupMember
        {
            TenantId = group.TenantId,
            PropertyGroupId = group.Id,
            PropertyId = propertyId,
            TypeCode = group.TypeCode,
            Period = MembershipPeriod.From(fromUtc),
        };

        member.Raise(new PropertyGroupMemberAddedDomainEvent(member));
        return member;
    }

    /// <summary>Closes the period. Ending an ended membership is refused by the handler first (00102); this is the last guard.</summary>
    public void End(DateTimeOffset toUtc)
    {
        if (!Period.IsCurrent)
        {
            throw new InvalidOperationException("A membership ends once.");
        }

        Period = Period.Ended(toUtc);
        Raise(new PropertyGroupMemberEndedDomainEvent(this));
    }
}
