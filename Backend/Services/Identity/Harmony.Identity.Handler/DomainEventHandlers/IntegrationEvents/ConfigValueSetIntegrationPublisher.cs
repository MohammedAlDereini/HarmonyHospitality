namespace Harmony.Identity.Handler.DomainEventHandlers.IntegrationEvents;

using Harmony.Core.Identity.Events;
using Harmony.Identity.Domain.DomainEvents.ConfigModule;
using Harmony.Identity.Domain.Services;

/// <summary>A customer set a value: its services drop what they keep of that key (event 1301).</summary>
public sealed class ConfigValueSetIntegrationPublisher(IIntegrationEventPublisher events) : INotificationHandler<ConfigValueSetDomainEvent>
{
    public Task Handle(ConfigValueSetDomainEvent notification, CancellationToken cancellationToken)
        => events.AddAsync(new SettingsChangedIntegrationEvent { TenantId = notification.TenantId, Key = notification.Key }, cancellationToken);
}
