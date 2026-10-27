namespace Harmony.Identity.Handler.DomainEventHandlers.IntegrationEvents;

using Harmony.Core.Identity.Events;
using Harmony.Identity.Domain.DomainEvents.TenantModule;
using Harmony.Identity.Domain.Services;

/// <summary>A customer suspended: the other services stop serving it (event 1001).</summary>
public sealed class TenantSuspendedIntegrationPublisher(IIntegrationEventPublisher events) : INotificationHandler<TenantSuspendedDomainEvent>
{
    public Task Handle(TenantSuspendedDomainEvent notification, CancellationToken cancellationToken)
        => events.AddAsync(new TenantSuspendedIntegrationEvent { TenantId = notification.TenantId, Reason = notification.Reason }, cancellationToken);
}
