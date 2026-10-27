using Harmony.Core.Abstractions;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Outbox.DI;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Messaging;

/// <summary>Messaging off (development without RabbitMQ, the test runs that do not ask for it): nothing leaves Identity.</summary>
internal sealed class SilentIntegrationEventPublisher(ILogger<SilentIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    public Task AddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        logger.LogDebug("Messaging is off ({Section} absent): {Event} is not published.", IdentityMessaging.Section, integrationEvent.GetType().Name);
        return Task.CompletedTask;
    }
}
