using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.DomainEvents.TenantModule;

/// <summary>The customer's contract changed: a line born, renewed, switched on or off. Raised once per save however many lines moved.</summary>
public sealed record TenantSubscriptionsChangedDomainEvent(Guid TenantId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
