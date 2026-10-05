namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class CountrySubdivisionModel : ReferenceModelBase
{
    public string Code { get; set; } = null!;

    public string CountryCode { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    /// <summary>Governorate, Region, Emirate, State...</summary>
    public string Kind { get; set; } = null!;
}
