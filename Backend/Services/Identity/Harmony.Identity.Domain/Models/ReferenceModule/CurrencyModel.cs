namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class CurrencyModel : ReferenceModelBase
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    /// <summary>From the framework Money registry, never typed.</summary>
    public int MinorUnits { get; set; }

    public bool IsActive { get; set; }
}
