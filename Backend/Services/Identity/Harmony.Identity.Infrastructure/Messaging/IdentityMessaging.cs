using Harmony.Core.Abstractions;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Outbox.DI;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Messaging;

/// <summary>
/// Identity's messaging, made of the framework's own parts (design: outbox/inbox, "use the framework as is"): the event
/// bus on RabbitMQ; events collected during a command and published after it; a failed publish kept in the database
/// outbox (schema Outbox, table OutboxContainerEntries) and retried by its Quartz jobs; the idempotent publisher and
/// consumer (the inbox) on Redis locks. On when the settings carry an EventBusSettings section; Program refuses to
/// start in Production without one, so events are never silently dropped there.
/// </summary>
public static class IdentityMessaging
{
    public const string Section = "EventBusSettings";

    /// <summary>Every 10 seconds the outbox job publishes what failed; every 5 minutes the failed-again job retries the rest.</summary>
    private const string OutboxEvery = "0/10 * * * * ? *";
    private const string FailedOutboxEvery = "0 */5 * ? * *";

    public static bool IsOn(IConfiguration configuration) => configuration.GetSection(Section).Exists();

    /// <summary>The bus, the outbox and the publisher Identity's handlers use; or, when off, a publisher that only logs.</summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        if (!IsOn(configuration))
        {
            services.AddScoped<IIntegrationEventPublisher, SilentIntegrationEventPublisher>();
            return services;
        }

        Harmony.Core.EventBus.DI.DependenciesConfigurator.AddEventBus(services, configuration, bus =>
        {
            bus.AddRabbitMQ();
            bus.AddIdempotentPublisher<IDistributedLockManager<Harmony.Core.Cache.Providers.Redis>>();
            bus.AddIdempotentConsumer<IDistributedLockManager<Harmony.Core.Cache.Providers.Redis>>();
        });

        services.AddOutbox(configuration, outbox => outbox.AddDatabaseOutbox(database =>
        {
            database.AddBusOutbox(OutboxEvery);
            database.AddBusFailedOutbox(FailedOutboxEvery);
        }));

        services.AddScoped<IIntegrationEventPublisher, EventBusIntegrationEventPublisher>();
        return services;
    }
}
