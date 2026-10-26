using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;

namespace Harmony.Identity.Domain.DomainEvents.GroupMembershipModule;

/// <summary>A hotel joined a group. Published by the framework after the save commits.</summary>
public sealed record PropertyGroupMemberAddedDomainEvent(PropertyGroupMember Member) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
