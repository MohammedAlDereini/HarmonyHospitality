using System.Text.RegularExpressions;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.DomainEvents.ConfigModule;

namespace Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>
/// A setting another service owns, registered here so every customer can set it once for the whole chain, a group or
/// one hotel (design 5.1, the ERD's ConfigSettings). Identity learns the key, its owner, its type word and a default; it
/// never learns what the value means and never parses one. Global, like the reference tables: no tenant. Registered and
/// changed by the platform. Fixed once saved: the key and the data type (a value written as an int must not turn into a time).
/// </summary>
public sealed partial class ConfigSetting : AggregateRoot<string>, IAuditableEntity, IConcurrentEntity
{
    public const int KeyMaxLength = 128;
    public const int OwnerServiceMaxLength = 64;
    public const int DataTypeMaxLength = 16;
    public const int DefaultMaxLength = 1000;
    public const int AppliesToTypesMaxLength = 400;
    public const int AppliesToTypesMaxCount = 10;
    public const int TypeCodeMaxLength = 32;

    /// <summary>The words a value's type may be: a hint for the owner and the screens, never checked by Identity.</summary>
    public static readonly IReadOnlyList<string> DataTypes = ["time", "int", "bool", "decimal", "string"];

    private ConfigSetting(string key)
        : base(key)
    {
    }

    private ConfigSetting()
    {
    }

    /// <summary>Like frontoffice.checkInTime: the owning area first, then dotted words. Unique in any case.</summary>
    public string Key => Id;

    /// <summary>The service that reads the value and decides what it means, like FrontOffice.</summary>
    public string OwnerService { get; private set; } = null!;

    /// <summary>time, int, bool, decimal or string. Fixed once saved.</summary>
    public string DataType { get; private set; } = null!;

    /// <summary>What wins when nobody set anything. Optional: no default means "not set" reaches the owner.</summary>
    public string? SystemDefault { get; private set; }

    /// <summary>Optional narrowing: the property type codes the setting is for, comma-separated, sorted; null means every type.</summary>
    public string? AppliesToTypes { get; private set; }

    /// <summary>Switched off: no value is set for it, and the readers stop seeing it.</summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    /// <summary>The narrowing as a list; empty means every type.</summary>
    public IReadOnlyList<string> TypeCodes
        => AppliesToTypes is null ? [] : AppliesToTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Is the setting for a hotel of this type.</summary>
    public bool AppliesTo(string propertyTypeCode)
        => AppliesToTypes is null || TypeCodes.Contains(propertyTypeCode, StringComparer.OrdinalIgnoreCase);

    public static ConfigSetting Register(string key, string ownerService, string dataType, string? systemDefault, IEnumerable<string>? appliesToTypes)
    {
        var setting = new ConfigSetting(SettingKey(key))
        {
            OwnerService = Owner(ownerService),
            DataType = TypeWord(dataType),
            SystemDefault = Default(systemDefault),
            AppliesToTypes = Types(appliesToTypes),
            IsActive = true,
        };

        setting.Raise(new ConfigSettingRegisteredDomainEvent(setting.Key, setting.OwnerService));
        return setting;
    }

    /// <summary>What may change: the owner, the default, the narrowing, on or off. Never the key or the data type.</summary>
    public void Update(string ownerService, string? systemDefault, IEnumerable<string>? appliesToTypes, bool isActive)
    {
        OwnerService = Owner(ownerService);
        SystemDefault = Default(systemDefault);
        AppliesToTypes = Types(appliesToTypes);
        IsActive = isActive;
        Raise(new ConfigSettingChangedDomainEvent(Key, OwnerService, IsActive));
    }

    /// <summary>The key as written, trimmed; the database compares it without case.</summary>
    public static string SettingKey(string? value)
    {
        var key = value?.Trim() ?? string.Empty;
        if (key.Length > KeyMaxLength || !KeyPattern().IsMatch(key))
        {
            throw new ArgumentException($"'{value}' is not a setting key: the owning area first, then dotted words, like frontoffice.checkInTime.", nameof(value));
        }

        return key;
    }

    private static string Owner(string? value)
    {
        var owner = value?.Trim() ?? string.Empty;
        if (!OwnerPattern().IsMatch(owner))
        {
            throw new ArgumentException($"'{value}' is not a service name: 2 to {OwnerServiceMaxLength} letters or digits.", nameof(value));
        }

        return owner;
    }

    private static string TypeWord(string? value)
    {
        var word = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!DataTypes.Contains(word))
        {
            throw new ArgumentException($"'{value}' is not a data type: {string.Join(", ", DataTypes)}.", nameof(value));
        }

        return word;
    }

    private static string? Default(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.Trim();
        if (text.Length > DefaultMaxLength)
        {
            throw new ArgumentException($"A default of at most {DefaultMaxLength} characters.", nameof(value));
        }

        return text.Length == 0 ? null : text;
    }

    private static string? Types(IEnumerable<string>? codes)
    {
        var list = (codes ?? [])
            .Select(code => code?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (list.Count > AppliesToTypesMaxCount || list.Any(code => code.Length is < 2 or > TypeCodeMaxLength || code.Contains(',')))
        {
            throw new ArgumentException($"At most {AppliesToTypesMaxCount} property type codes of 2 to {TypeCodeMaxLength} characters.", nameof(codes));
        }

        return list.Count == 0 ? null : string.Join(',', list);
    }

    // Matches "frontoffice.checkInTime" and "pms.night-audit.hour"; rejects "FrontOffice.x" (area starts lower-case),
    // "frontoffice" (no dot) and "a..b". Tester: https://regex101.com/?regex=%5E%5Ba-z%5D%5Ba-z0-9%5D*(%5C.%5Ba-zA-Z%5D%5Ba-zA-Z0-9-%5D*)%2B%24
    [GeneratedRegex("^[a-z][a-z0-9]*(\\.[a-zA-Z][a-zA-Z0-9-]*)+$")]
    private static partial Regex KeyPattern();

    // Matches "FrontOffice" and "Pms2"; rejects "2Pms", "Front Office" and "F".
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]{1,63}$")]
    private static partial Regex OwnerPattern();
}
