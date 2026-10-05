namespace Harmony.Identity.Domain.Models.ReferenceModule;

public class CityModel : ReferenceModelBase
{
    public Guid Id { get; set; }

    public string CountryCode { get; set; } = null!;

    public string? SubdivisionCode { get; set; }

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string TimeZoneId { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public bool IsActive { get; set; }
}
