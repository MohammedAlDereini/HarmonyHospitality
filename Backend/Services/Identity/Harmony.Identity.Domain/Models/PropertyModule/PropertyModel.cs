namespace Harmony.Identity.Domain.Models.PropertyModule;

/// <summary>A hotel as the platform console sees it. The lines and the computed facts are filled on the detail read only.</summary>
public class PropertyModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = null!;

    public string? ExternalIdentifier { get; set; }

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string PropertyTypeCode { get; set; } = null!;

    public Guid CityId { get; set; }

    public string CountryCode { get; set; } = null!;

    public string? SubdivisionCode { get; set; }

    public string TimeZoneId { get; set; } = null!;

    public string AddressLine { get; set; } = null!;

    public string? PostalCode { get; set; }

    public string DefaultCurrency { get; set; } = null!;

    public string DefaultLanguageCode { get; set; } = null!;

    public int TotalRooms { get; set; }

    public string LegalEnvironmentCode { get; set; } = null!;

    public string TaxEnvironmentCode { get; set; } = null!;

    public string AccountingEnvironmentCode { get; set; } = null!;

    public string AccommodationEnvironmentCode { get; set; } = null!;

    public DateOnly? OpensOn { get; set; }

    public DateOnly? ClosesOn { get; set; }

    public bool IsActive { get; set; }

    public string? ShortName { get; set; }

    public string? UniqueName { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? BusinessId { get; set; }

    public string? BusinessRegistrationCode { get; set; }

    public string? BusinessPremiseId { get; set; }

    public int? FiscalYearBeginMonth { get; set; }

    public int? FiscalYearBeginDay { get; set; }

    public string? DefaultCulture { get; set; }

    public int? StarRating { get; set; }

    public string? Email { get; set; }

    public string? Telephone { get; set; }

    public string? WebsiteUrl { get; set; }

    public string? ProductChampionEmail { get; set; }

    public string? LegalOwnerName { get; set; }

    public int? DisplayOrder { get; set; }

    /// <summary>Active, opened and not yet closed on the hotel's own local day. Detail only.</summary>
    public bool? IsOpenToday { get; set; }

    /// <summary>The most rooms any held line licenses at this hotel; null when none carries rooms. Detail only.</summary>
    public int? LicensedRooms { get; set; }

    /// <summary>TotalRooms above LicensedRooms: flagged for finance, never refused. Detail only.</summary>
    public bool RoomsAboveLicence { get; set; }

    /// <summary>This hotel's lines. Detail only.</summary>
    public List<SubscriptionLineModel> Subscriptions { get; set; } = [];

    public DateTime CreatedDate { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? ModifiedDate { get; set; }

    public string? ModifiedBy { get; set; }
}
