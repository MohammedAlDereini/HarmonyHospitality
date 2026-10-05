namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class CapabilityModel : ReferenceModelBase
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string OwnerService { get; set; } = null!;

    /// <summary>True when an entitlement of this capability carries a number (rooms, properties, users).</summary>
    public bool HasLimit { get; set; }
}
