namespace Harmony.Identity.Handler.DomainEventHandlers.TenantModule;

using Harmony.Identity.Domain.DomainEvents.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;

/// <summary>Suspended: the publisher writes an empty list for a customer that is not active, so every service stops serving its hotels.</summary>
public sealed class TenantSuspendedDomainEventHandler : TenantDomainEventHandlerBase, INotificationHandler<TenantSuspendedDomainEvent>
{
    public TenantSuspendedDomainEventHandler(ITenantRepository tenants, ISubscriptionPublisher publisher, ILogger<TenantSuspendedDomainEventHandler> logger)
        : base(tenants, publisher, logger)
    {
    }

    public Task Handle(TenantSuspendedDomainEvent notification, CancellationToken cancellationToken)
        => this.PublishSubscriptionsAsync(notification.TenantId, cancellationToken);
}
