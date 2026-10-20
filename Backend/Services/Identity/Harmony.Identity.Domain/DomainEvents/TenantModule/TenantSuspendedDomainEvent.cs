using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.DomainEvents.TenantModule;

/// <summary>The customer was suspended: nobody in it signs in, and its hotels are served nothing until it is reinstated.</summary>
public sealed record TenantSuspendedDomainEvent(Guid TenantId, string Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
