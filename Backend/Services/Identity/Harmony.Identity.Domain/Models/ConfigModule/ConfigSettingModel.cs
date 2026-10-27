namespace Harmony.Identity.Domain.Models.ConfigModule;

/// <summary>One registered setting (ConfigSettings).</summary>
public class ConfigSettingModel
{
    public string Key { get; set; } = null!;

    public string OwnerService { get; set; } = null!;

    public string DataType { get; set; } = null!;

    public string? SystemDefault { get; set; }

    /// <summary>The property type codes it is narrowed to; empty means every type.</summary>
    public List<string> AppliesToTypes { get; set; } = [];

    public bool IsActive { get; set; }

    /// <summary>The stored narrowing ("HOTEL,RESORT"), read by the query and split into AppliesToTypes after paging.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Narrowing { get; set; }

    /// <summary>Fills AppliesToTypes from Narrowing once the rows are read.</summary>
    public ConfigSettingModel Split()
    {
        this.AppliesToTypes = this.Narrowing is null ? [] : this.Narrowing.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        return this;
    }
}
