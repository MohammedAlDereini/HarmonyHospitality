namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class PropertyTypeModel : ReferenceModelBase
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }
}
