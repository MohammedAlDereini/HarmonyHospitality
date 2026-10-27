namespace Harmony.Identity.Domain.Models.ConfigModule;

/// <summary>What a key resolves to at one hotel, and how (the trace).</summary>
public class EffectiveSettingModel
{
    public string Key { get; set; } = null!;

    public string OwnerService { get; set; } = null!;

    public string DataType { get; set; } = null!;

    /// <summary>Value, Unset, Conflict or NotApplicable.</summary>
    public string Outcome { get; set; } = null!;

    /// <summary>The winning value; null unless the outcome is Value.</summary>
    public string? Value { get; set; }

    /// <summary>Property, PropertyGroup, Tenant or SystemDefault; null when nothing won.</summary>
    public string? Source { get; set; }

    /// <summary>The winning hotel's or group's code.</summary>
    public string? SourceCode { get; set; }

    public List<ResolutionStepModel> Trace { get; set; } = [];
}
