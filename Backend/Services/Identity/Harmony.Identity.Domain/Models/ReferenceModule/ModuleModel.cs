using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class ModuleModel : ReferenceModelBase
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string OwnerService { get; set; } = null!;

    /// <summary>Property: sold per hotel. Tenant: sold once for the whole customer.</summary>
    public ModuleLevel Level { get; set; }

    /// <summary>True when a subscription line of this module carries a number (rooms, terminals).</summary>
    public bool HasLimit { get; set; }
}
