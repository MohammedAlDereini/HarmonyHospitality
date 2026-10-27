using Harmony.Core.Abstractions;

namespace Harmony.Identity.Domain.Services;

/// <summary>
/// What Identity tells the other services. An event added during a command is published when the command is done
/// (the framework's event bus: publish after the commit; a failed publish is kept in the framework's outbox and retried).
/// With messaging off (no EventBusSettings, never in Production) nothing is published and the event is only logged.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task AddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
