namespace Harmony.Identity.Domain.Models.ConfigModule;

/// <summary>One line of a trace.</summary>
public class ResolutionStepModel
{
    /// <summary>Property, PropertyGroup, Tenant or SystemDefault.</summary>
    public string Level { get; set; } = null!;

    /// <summary>The hotel's or the group's id, so a screen can set a value there; null for the customer and the default.</summary>
    public Guid? Id { get; set; }

    public string? Code { get; set; }

    public int? Precedence { get; set; }

    public string? Value { get; set; }

    /// <summary>Won, Overridden, NotSet, Tied, Outranked or Inactive.</summary>
    public string Verdict { get; set; } = null!;
}
