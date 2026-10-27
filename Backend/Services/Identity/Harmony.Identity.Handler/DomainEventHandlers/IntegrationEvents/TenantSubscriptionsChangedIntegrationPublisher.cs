namespace Harmony.Identity.Handler.DomainEventHandlers.IntegrationEvents;

using Harmony.Core.Identity.Events;
using Harmony.Identity.Domain.DomainEvents.TenantModule;
using Harmony.Identity.Domain.Services;

/// <summary>A customer's subscription lines changed: read them again (event 1003).</summary>
public sealed class TenantSubscriptionsChangedIntegrationPublisher(IIntegrationEventPublisher events) : INotificationHandler<TenantSubscriptionsChangedDomainEvent>
{
    public Task Handle(TenantSubscriptionsChangedDomainEvent notification, CancellationToken cancellationToken)
        => events.AddAsync(new TenantSubscriptionsChangedIntegrationEvent { TenantId = notification.TenantId }, cancellationToken);
}
