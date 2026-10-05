using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class RegulatoryEnvironmentModel : ReferenceModelBase
{
    public string Code { get; set; } = null!;

    public RegulatoryKind Kind { get; set; }

    public string CountryCode { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string OwnerService { get; set; } = null!;
}
