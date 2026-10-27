namespace Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>Where a value is set: the whole customer, one group of its hotels, or one hotel. Stored as the ERD's int Scope.</summary>
public enum ConfigScopeLevel
{
    Tenant = 1,
    PropertyGroup = 2,
    Property = 3,
}

/// <summary>
/// The place a value is set (the ERD's Scope + ScopeId). For the whole customer the id is the customer's own tenant id,
/// so one unique index holds every level. No foreign key can follow a polymorphic id: the handler proves the hotel or
/// the group is the customer's before a value lands.
/// </summary>
public sealed record ConfigScope
{
    private ConfigScope(ConfigScopeLevel level, Guid id)
    {
        Level = level;
        Id = id;
    }

    public ConfigScopeLevel Level { get; }

    /// <summary>The tenant's, the group's or the hotel's id.</summary>
    public Guid Id { get; }

    public static ConfigScope Tenant(Guid tenantId) => new(ConfigScopeLevel.Tenant, Required(tenantId, nameof(tenantId)));

    public static ConfigScope Group(Guid propertyGroupId) => new(ConfigScopeLevel.PropertyGroup, Required(propertyGroupId, nameof(propertyGroupId)));

    public static ConfigScope Property(Guid propertyId) => new(ConfigScopeLevel.Property, Required(propertyId, nameof(propertyId)));

    public static ConfigScope Of(ConfigScopeLevel level, Guid id)
        => level switch
        {
            ConfigScopeLevel.Tenant => Tenant(id),
            ConfigScopeLevel.PropertyGroup => Group(id),
            ConfigScopeLevel.Property => Property(id),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Tenant, PropertyGroup or Property."),
        };

    private static Guid Required(Guid id, string argument)
        => id == Guid.Empty ? throw new ArgumentException("An id is required.", argument) : id;
}
