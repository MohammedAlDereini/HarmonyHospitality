using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;

namespace Harmony.Identity.Domain.DomainEvents.PropertyModule;

/// <summary>
/// A hotel was born. Raised by Property.Create and published by the framework after the save commits, so a handler
/// that reacts can never run for a hotel that was not saved. Carries the hotel itself, the way the reference Order
/// event carries the Order: its id is assigned when the context tracks it, not before, so there is no id to carry.
/// </summary>
public sealed record PropertyCreatedDomainEvent(Property Property) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
