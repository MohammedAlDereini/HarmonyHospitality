namespace Harmony.Identity.Handler.DomainEventHandlers.TenantModule;

using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// What the customer's event handlers share: republishing its lines after the save has committed. The customer is
/// reloaded with its lines in the same scope, so a line added on a customer loaded without its lines is never published
/// alone. A cache that cannot be written is logged, never thrown: the rows are saved, the next Identity start republishes.
/// </summary>
public abstract class TenantDomainEventHandlerBase
{
    private readonly ITenantRepository tenants;
    private readonly ISubscriptionPublisher publisher;
    private readonly ILogger logger;

    protected TenantDomainEventHandlerBase(ITenantRepository tenants, ISubscriptionPublisher publisher, ILogger logger)
    {
        this.tenants = tenants;
        this.publisher = publisher;
        this.logger = logger;
    }

    protected async Task PublishSubscriptionsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await this.tenants.FindWithSubscriptionsAsync(tenantId, cancellationToken)
                ?? throw new InvalidOperationException($"Tenant {tenantId} vanished after its save.");
            await this.publisher.PublishAsync(tenant, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this.logger.LogError(exception, "Subscription lines of tenant {TenantId} were saved but not published; the next start republishes them.", tenantId);
        }
    }
}
