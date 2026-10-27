namespace Harmony.Identity.Handler.DomainEventHandlers.IntegrationEvents;

using Harmony.Core.Identity.Events;
using Harmony.Identity.Domain.DomainEvents.TenantModule;
using Harmony.Identity.Domain.Services;

/// <summary>A customer reinstated: served again (event 1002).</summary>
public sealed class TenantReinstatedIntegrationPublisher(IIntegrationEventPublisher events) : INotificationHandler<TenantReinstatedDomainEvent>
{
    public Task Handle(TenantReinstatedDomainEvent notification, CancellationToken cancellationToken)
        => events.AddAsync(new TenantReinstatedIntegrationEvent { TenantId = notification.TenantId }, cancellationToken);
}
