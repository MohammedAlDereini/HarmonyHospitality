namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class PropertyGroupTypeModel : ReferenceModelBase
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    /// <summary>A property may belong to one group of an exclusive type (one brand per hotel).</summary>
    public bool IsExclusive { get; set; }

    /// <summary>Code relies on this row: it can be renamed, its exclusivity cannot change.</summary>
    public bool IsSystem { get; set; }
}
