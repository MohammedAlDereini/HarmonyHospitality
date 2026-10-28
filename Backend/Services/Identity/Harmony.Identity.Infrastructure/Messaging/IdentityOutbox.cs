using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Core.Identity.Events;
using Harmony.Core.Models;
using Harmony.Identity.Domain.DomainEvents.ConfigModule;
using Harmony.Identity.Domain.DomainEvents.PropertyModule;
using Harmony.Identity.Domain.DomainEvents.TenantModule;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Harmony.Identity.Infrastructure.Messaging;

/// <summary>
/// Stage F, the transactional outbox (Microsoft: write the event in the same local transaction as the data). Before Identity
/// saves, every domain event that other services must hear about becomes a row of the framework's outbox table in the SAME
/// save: the change and its event commit together or not at all. The framework's outbox job sends the rows in order and
/// marks them sent once RabbitMQ confirmed them. With messaging off (no outbox settings) nothing is written.
/// </summary>
internal static class IdentityOutbox
{
    /// <summary>Adds the outbox rows for the events not written yet (a save called again never writes one twice).</summary>
    public static void Stage(IdentityDbContext context, HashSet<IDomainEvent> written)
    {
        if (context.Model.FindEntityType(typeof(OutboxContainerEntry)) is null)
        {
            return;
        }

        var callContext = context.ContextServices?.GetService<ICallContext>();
        var application = context.ContextServices?.GetService<AppInfo>()?.Name ?? "Identity";
        var tenantId = Guid.TryParse(callContext?.TenantId(), out var tenant) ? tenant : Guid.Empty;
        var actor = callContext?.UserId();

        var events = context.ChangeTracker.Entries<IHasDomainEvents>().SelectMany(e => e.Entity.DomainEvents).ToList();
        foreach (var domainEvent in events)
        {
            if (written.Add(domainEvent) && IntegrationEventOf(domainEvent) is { } integrationEvent)
            {
                context.Add(OutboxContainerEntry.ForEvent(integrationEvent, tenantId, application, actor!, callContext!));
            }
        }
    }

    /// <summary>What Identity tells the other services, per domain event (exchange harmony.identity, types 1001 to 1301).</summary>
    internal static IntegrationEvent? IntegrationEventOf(IDomainEvent domainEvent) => domainEvent switch
    {
        // A customer suspended or back: the other services stop or start serving it (1001, 1002).
        TenantSuspendedDomainEvent e => new TenantSuspendedIntegrationEvent { TenantId = e.TenantId, Reason = e.Reason },
        TenantReinstatedDomainEvent e => new TenantReinstatedIntegrationEvent { TenantId = e.TenantId },

        // A customer's subscription lines changed: the services re-read them (1003).
        TenantSubscriptionsChangedDomainEvent e => new TenantSubscriptionsChangedIntegrationEvent { TenantId = e.TenantId },

        // A hotel born for a customer: the services that keep per-hotel data make its place (1101).
        PropertyCreatedDomainEvent e => new PropertyCreatedIntegrationEvent
        {
            TenantId = e.Property.TenantId,
            PropertyId = e.Property.Id,
            Code = e.Property.Code,
            PropertyTypeCode = e.Property.PropertyTypeCode,
            DefaultCurrency = e.Property.DefaultCurrency,
            TotalRooms = e.Property.TotalRooms,
        },

        // A setting's definition or a customer's value changed: the services drop what they keep of that key (1301).
        ConfigSettingChangedDomainEvent e => new SettingsChangedIntegrationEvent { Key = e.Key, EveryCustomer = true },
        ConfigValueSetDomainEvent e => new SettingsChangedIntegrationEvent { TenantId = e.TenantId, Key = e.Key },
        ConfigValueClearedDomainEvent e => new SettingsChangedIntegrationEvent { TenantId = e.TenantId, Key = e.Key },
        _ => null,
    };
}
