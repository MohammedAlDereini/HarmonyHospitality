using Harmony.Core.Abstractions;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Outbox.DI;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Messaging;

/// <summary>
/// Identity's messaging, made of the framework's own parts ("use the framework as is"): the event bus on RabbitMQ with
/// publisher confirms, and the database outbox (schema Outbox, table OutboxContainerEntries). Stage F: Identity writes its
/// events into that table in the same save as the data (IdentityOutbox); the framework's job sends them in order and marks
/// them sent once RabbitMQ confirmed them. Receivers skip repeats through the inbox (IInboxRepository). On when the
/// settings carry an EventBusSettings section; Program refuses to start in Production without one, so events are never
/// silently dropped there. Off: no outbox table, nothing is written or sent.
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

        return services;
    }
}
