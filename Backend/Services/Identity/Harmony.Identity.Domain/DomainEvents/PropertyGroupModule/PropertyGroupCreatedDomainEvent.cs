using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;

namespace Harmony.Identity.Domain.DomainEvents.PropertyGroupModule;

/// <summary>
/// A hotel group was born. Raised by PropertyGroup.Create and published by the framework after the save commits,
/// so a handler that reacts can never run for a group that was not saved. Carries the group itself: its id is
/// assigned when the context tracks it, not before.
/// </summary>
public sealed record PropertyGroupCreatedDomainEvent(PropertyGroup Group) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
