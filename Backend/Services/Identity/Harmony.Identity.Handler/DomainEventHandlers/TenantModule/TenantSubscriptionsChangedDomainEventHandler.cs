namespace Harmony.Identity.Handler.DomainEventHandlers.TenantModule;

using Harmony.Identity.Domain.DomainEvents.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;

/// <summary>Lines moved: the customer's key is rewritten whole.</summary>
public sealed class TenantSubscriptionsChangedDomainEventHandler : TenantDomainEventHandlerBase, INotificationHandler<TenantSubscriptionsChangedDomainEvent>
{
    public TenantSubscriptionsChangedDomainEventHandler(ITenantRepository tenants, ISubscriptionPublisher publisher, ILogger<TenantSubscriptionsChangedDomainEventHandler> logger)
        : base(tenants, publisher, logger)
    {
    }

    public Task Handle(TenantSubscriptionsChangedDomainEvent notification, CancellationToken cancellationToken)
        => this.PublishSubscriptionsAsync(notification.TenantId, cancellationToken);
}
