using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

namespace Harmony.Identity.Domain.DomainEvents.ConfigModule;

/// <summary>A service's key was registered. Published by the framework after the save commits.</summary>
public sealed record ConfigSettingRegisteredDomainEvent(string Key, string OwnerService) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}

/// <summary>A registered key changed its owner, its default, its narrowing, or was switched on or off.</summary>
public sealed record ConfigSettingChangedDomainEvent(string Key, string OwnerService, bool IsActive) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}

/// <summary>A customer set (or changed) a value at a place. Carries no value: a reader asks for the winner, never trusts a copy.</summary>
public sealed record ConfigValueSetDomainEvent(Guid TenantId, string Key, ConfigScopeLevel Scope, Guid ScopeId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}

/// <summary>A customer cleared a value at a place: the next lower place wins again.</summary>
public sealed record ConfigValueClearedDomainEvent(Guid TenantId, string Key, ConfigScopeLevel Scope, Guid ScopeId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
