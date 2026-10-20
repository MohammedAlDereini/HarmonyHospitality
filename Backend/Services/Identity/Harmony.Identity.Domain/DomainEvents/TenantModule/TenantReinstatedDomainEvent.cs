using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.DomainEvents.TenantModule;

/// <summary>The customer is back: its people sign in again and its lines are served again.</summary>
public sealed record TenantReinstatedDomainEvent(Guid TenantId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
