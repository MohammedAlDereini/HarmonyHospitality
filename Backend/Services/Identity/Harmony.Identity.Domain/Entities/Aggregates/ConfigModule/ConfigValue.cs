using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.DomainEvents.ConfigModule;

namespace Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>
/// Something a customer set for a registered key: for the whole customer, for one group of its hotels, or for one hotel
/// (design 5.1, the ERD's ConfigValues). Inside the customer's tenant (the wall). A string, stored as given and never
/// parsed here: the owning service decides what it means. One value per key per place; clearing removes the row.
/// </summary>
public sealed class ConfigValue : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int ValueMaxLength = 1000;

    private ConfigValue()
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>The ERD's Scope: whole customer, a group, or a hotel.</summary>
    public ConfigScopeLevel Scope { get; private set; }

    /// <summary>The ERD's ScopeId: the tenant's own id at the customer level.</summary>
    public Guid ScopeId { get; private set; }

    /// <summary>A key of ConfigSettings.</summary>
    public string Key { get; private set; } = null!;

    public string Value { get; private set; } = null!;

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    /// <summary>The place, as the value object the resolver reads.</summary>
    public ConfigScope Place => ConfigScope.Of(Scope, ScopeId);

    /// <summary>
    /// A first value for a key at a place. The tenant is set here, explicitly; at the customer level the place is the
    /// tenant itself. Raises ConfigValueSetDomainEvent, published by the framework after the save commits.
    /// </summary>
    public static ConfigValue Set(Guid tenantId, ConfigScope place, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(place);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant is required.", nameof(tenantId));
        }

        if (place.Level == ConfigScopeLevel.Tenant && place.Id != tenantId)
        {
            throw new ArgumentException("A customer-wide value is placed on the customer's own tenant.", nameof(place));
        }

        var row = new ConfigValue
        {
            TenantId = tenantId,
            Scope = place.Level,
            ScopeId = place.Id,
            Key = ConfigSetting.SettingKey(key),
            Value = Text(value),
        };

        row.Raise(new ConfigValueSetDomainEvent(tenantId, row.Key, row.Scope, row.ScopeId));
        return row;
    }

    /// <summary>A new value at the same place. Nothing is raised when the value does not change.</summary>
    public void Change(string value)
    {
        var text = Text(value);
        if (string.Equals(text, Value, StringComparison.Ordinal))
        {
            return;
        }

        Value = text;
        Raise(new ConfigValueSetDomainEvent(TenantId, Key, Scope, ScopeId));
    }

    /// <summary>The value goes: the next lower place wins again. The handler removes the row in the same save.</summary>
    public void Clear() => Raise(new ConfigValueClearedDomainEvent(TenantId, Key, Scope, ScopeId));

    private static string Text(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is 0 or > ValueMaxLength)
        {
            throw new ArgumentException($"A value of 1 to {ValueMaxLength} characters is required.", nameof(value));
        }

        return text;
    }
}
