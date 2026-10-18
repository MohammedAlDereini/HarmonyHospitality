namespace Harmony.Identity.Handler.Events.PropertyModule;

using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// After a hotel is born (the framework publishes the event once the save has committed), the customer's lines go to
/// the cache. A cache that cannot be written is logged, never thrown: the rows are saved, the hotel exists, and the
/// next Identity start or heal republishes every customer.
/// </summary>
public sealed class PublishSubscriptionsWhenPropertyCreated : INotificationHandler<PropertyCreatedDomainEvent>
{
    private readonly ITenantRepository tenants;
    private readonly ISubscriptionPublisher publisher;
    private readonly ILogger<PublishSubscriptionsWhenPropertyCreated> logger;

    public PublishSubscriptionsWhenPropertyCreated(ITenantRepository tenants, ISubscriptionPublisher publisher, ILogger<PublishSubscriptionsWhenPropertyCreated> logger)
    {
        this.tenants = tenants;
        this.publisher = publisher;
        this.logger = logger;
    }

    public async Task Handle(PropertyCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var tenantId = notification.Property.TenantId;
        try
        {
            // The same scope that saved: the customer and its lines are already tracked, this is a cheap read.
            var tenant = await this.tenants.FindWithSubscriptionsAsync(tenantId, cancellationToken)
                ?? throw new InvalidOperationException($"Tenant {tenantId} vanished after its hotel was saved.");
            await this.publisher.PublishAsync(tenant, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this.logger.LogError(exception, "Subscription lines of tenant {TenantId} were saved but not published; the next start republishes them.", tenantId);
        }
    }
}
