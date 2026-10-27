namespace Harmony.Identity.Handler.DomainEventHandlers.IntegrationEvents;

using Harmony.Core.Identity.Events;
using Harmony.Identity.Domain.DomainEvents.ConfigModule;
using Harmony.Identity.Domain.Services;

/// <summary>A customer cleared a value (event 1301).</summary>
public sealed class ConfigValueClearedIntegrationPublisher(IIntegrationEventPublisher events) : INotificationHandler<ConfigValueClearedDomainEvent>
{
    public Task Handle(ConfigValueClearedDomainEvent notification, CancellationToken cancellationToken)
        => events.AddAsync(new SettingsChangedIntegrationEvent { TenantId = notification.TenantId, Key = notification.Key }, cancellationToken);
}
