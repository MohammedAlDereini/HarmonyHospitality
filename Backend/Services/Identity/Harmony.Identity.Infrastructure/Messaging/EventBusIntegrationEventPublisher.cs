using Harmony.Core.Abstractions;
using Harmony.Core.Cache.Abstractions;
using Harmony.Core.Outbox.DI;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.Messaging;

/// <summary>The framework's integration event service: the event waits for the end of the command, then goes to the bus.</summary>
internal sealed class EventBusIntegrationEventPublisher(IIntegrationEventService events) : IIntegrationEventPublisher
{
    public Task AddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
        => events.AddEventAsync(integrationEvent, cancellationToken);
}
