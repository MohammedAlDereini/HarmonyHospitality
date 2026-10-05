namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class CountryModel : ReferenceModelBase
{
    public string Code { get; set; } = null!;

    public string Alpha3 { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string DialCode { get; set; } = null!;

    public string DefaultCurrency { get; set; } = null!;

    public bool IsActive { get; set; }
}
