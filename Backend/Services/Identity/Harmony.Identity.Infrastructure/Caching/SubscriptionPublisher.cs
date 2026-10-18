using Harmony.Core.Identity.Subscriptions;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Services;

namespace Harmony.Identity.Infrastructure.Caching;

/// <summary>
/// Global:identity:subscriptions:{tenant} = every line of the customer as the shared SubscriptionLine record, rewritten
/// whole on every change. Readers decide "held" by date themselves, so nothing has to wake up at midnight.
/// </summary>
public sealed class SubscriptionPublisher : ISubscriptionPublisher
{
    private readonly ICacheService cacheService;

    public SubscriptionPublisher(ICacheService cacheService)
    {
        this.cacheService = cacheService;
    }

    public async Task PublishAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        var lines = tenant.Subscriptions
            .Select(s => new SubscriptionLine(s.PropertyId, s.ModuleCode, s.IsEnabled, s.RenewsAutomatically, s.Rooms, s.Terminals, s.StartsOn, s.EndsOn))
            .ToList();

        await this.cacheService.AddAsync(SubscriptionCacheKeys.TenantKey(tenant.Id), lines, SubscriptionCacheKeys.Lifetime, cancellationToken);
    }
}
