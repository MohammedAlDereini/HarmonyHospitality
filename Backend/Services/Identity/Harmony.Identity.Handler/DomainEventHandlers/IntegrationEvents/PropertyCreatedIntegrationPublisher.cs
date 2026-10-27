namespace Harmony.Identity.Handler.DomainEventHandlers.IntegrationEvents;

using Harmony.Core.Identity.Events;
using Harmony.Identity.Domain.DomainEvents.PropertyModule;
using Harmony.Identity.Domain.Services;

/// <summary>A hotel born for a customer (event 1101): the services that keep per-hotel data make its place.</summary>
public sealed class PropertyCreatedIntegrationPublisher(IIntegrationEventPublisher events) : INotificationHandler<PropertyCreatedDomainEvent>
{
    public Task Handle(PropertyCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var property = notification.Property;
        return events.AddAsync(
            new PropertyCreatedIntegrationEvent
            {
                TenantId = property.TenantId,
                PropertyId = property.Id,
                Code = property.Code,
                PropertyTypeCode = property.PropertyTypeCode,
                DefaultCurrency = property.DefaultCurrency,
                TotalRooms = property.TotalRooms,
            },
            cancellationToken);
    }
}
