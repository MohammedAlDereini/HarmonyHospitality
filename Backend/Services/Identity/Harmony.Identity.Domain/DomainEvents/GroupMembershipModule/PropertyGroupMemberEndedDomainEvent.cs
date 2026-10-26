using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;

namespace Harmony.Identity.Domain.DomainEvents.GroupMembershipModule;

/// <summary>A hotel's membership in a group was closed. Published by the framework after the save commits.</summary>
public sealed record PropertyGroupMemberEndedDomainEvent(PropertyGroupMember Member) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
