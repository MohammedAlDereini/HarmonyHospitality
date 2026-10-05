namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class TimeZoneModel : ReferenceModelBase
{
    /// <summary>IANA id, like Asia/Amman.</summary>
    public string Id { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public bool IsActive { get; set; }
}
