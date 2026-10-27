namespace Harmony.Identity.Handler.DomainEventHandlers.IntegrationEvents;

using Harmony.Core.Identity.Events;
using Harmony.Identity.Domain.DomainEvents.ConfigModule;
using Harmony.Identity.Domain.Services;

/// <summary>The registry changed a key (default, narrowing, on or off): every customer may resolve it differently (event 1301).</summary>
public sealed class ConfigSettingChangedIntegrationPublisher(IIntegrationEventPublisher events) : INotificationHandler<ConfigSettingChangedDomainEvent>
{
    public Task Handle(ConfigSettingChangedDomainEvent notification, CancellationToken cancellationToken)
        => events.AddAsync(new SettingsChangedIntegrationEvent { Key = notification.Key, EveryCustomer = true }, cancellationToken);
}
