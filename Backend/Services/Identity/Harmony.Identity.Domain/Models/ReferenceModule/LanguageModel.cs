namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class LanguageModel : ReferenceModelBase
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameNative { get; set; } = null!;

    public bool IsRightToLeft { get; set; }

    public bool IsActive { get; set; }
}
