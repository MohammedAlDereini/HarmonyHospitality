using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;

namespace Harmony.Identity.Domain.Services;

/// <summary>Writes a customer's subscription lines where the services read them. The database is the truth; the cache follows it.</summary>
public interface ISubscriptionPublisher
{
    /// <summary>Publishes every line of the tenant (its Subscriptions must be loaded). Throws when the cache cannot be written.</summary>
    Task PublishAsync(Tenant tenant, CancellationToken cancellationToken);
}
