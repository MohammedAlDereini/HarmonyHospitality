namespace Harmony.Identity.Handler.DomainEventHandlers.TenantModule;

using Harmony.Identity.Domain.DomainEvents.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;

/// <summary>Reinstated: the full list is served again.</summary>
public sealed class TenantReinstatedDomainEventHandler : TenantDomainEventHandlerBase, INotificationHandler<TenantReinstatedDomainEvent>
{
    public TenantReinstatedDomainEventHandler(ITenantRepository tenants, ISubscriptionPublisher publisher, ILogger<TenantReinstatedDomainEventHandler> logger)
        : base(tenants, publisher, logger)
    {
    }

    public Task Handle(TenantReinstatedDomainEvent notification, CancellationToken cancellationToken)
        => this.PublishSubscriptionsAsync(notification.TenantId, cancellationToken);
}
