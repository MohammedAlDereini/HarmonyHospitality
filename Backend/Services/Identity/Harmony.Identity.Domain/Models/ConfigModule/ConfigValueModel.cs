namespace Harmony.Identity.Domain.Models.ConfigModule;

/// <summary>One value a customer set (ConfigValues), with the place named in words.</summary>
public class ConfigValueModel
{
    public Guid Id { get; set; }

    public string Key { get; set; } = null!;

    /// <summary>Tenant, PropertyGroup or Property.</summary>
    public string Scope { get; set; } = null!;

    public Guid ScopeId { get; set; }

    /// <summary>The hotel's or the group's code; the customer's code at the customer level.</summary>
    public string ScopeCode { get; set; } = null!;

    public string Value { get; set; } = null!;

    public DateTime? ModifiedDate { get; set; }
}
