using System.Text.RegularExpressions;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.DomainEvents.PropertyModule;
using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;

/// <summary>
/// One hotel of a customer, inside the customer's tenant (the wall): its identity, its place, its money and language,
/// its four regimes, its opening dates and the rooms it really has. What it pays for is not here: those are the
/// customer's subscription lines, which name the hotel. Born by the platform together with its first lines; from then
/// on the hotel's own people read it and, later, edit it.
/// Fixed once saved: the code, the location (city, country, time zone) and the regimes, the way Mews fixes a
/// property's time zone and legal environment at creation. TotalRooms is what the hotel has, never what it licenses:
/// the gap is flagged, never refused.
/// </summary>
public sealed partial class Property : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 128;
    public const int ExternalIdentifierMaxLength = 64;
    public const int ShortNameMaxLength = 64;
    public const int UniqueNameMaxLength = 128;
    public const int BusinessTextMaxLength = 64;
    public const int DefaultCultureMaxLength = 16;
    public const int EmailMaxLength = 256;
    public const int TelephoneMaxLength = 32;
    public const int WebsiteUrlMaxLength = 512;
    public const int LegalOwnerNameMaxLength = 128;

    /// <summary>The hotel star scale runs 1 to 5 (HotelStars Union, AAA, the national schemes); "six" and "seven" stars are marketing.</summary>
    public const int StarRatingMax = 5;

    private Property()
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>The short name we and the hotel call it by, like AMMAN. Chosen once; upper-case; unique in the tenant.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>The hotel's reference in the system that sends the bill. Optional, ours to read.</summary>
    public string? ExternalIdentifier { get; private set; }

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    /// <summary>A row of PropertyTypes (hotel, resort, hostel...).</summary>
    public string PropertyTypeCode { get; private set; } = null!;

    public Location Location { get; private set; } = null!;

    public PostalAddress Address { get; private set; } = null!;

    /// <summary>ISO 4217, a row of Currencies: the hotel's books.</summary>
    public string DefaultCurrency { get; private set; } = null!;

    /// <summary>ISO 639-1, a row of Languages, lower-case like the table.</summary>
    public string DefaultLanguageCode { get; private set; } = null!;

    /// <summary>The rooms the hotel really has. Compared with the licensed rooms on its lines; a gap is flagged, never refused.</summary>
    public int TotalRooms { get; private set; }

    public RegulatoryProfile Regulatory { get; private set; } = null!;

    /// <summary>Accepts reservations from this local day; null = open now (Opera: Begin Date).</summary>
    public DateOnly? OpensOn { get; private set; }

    /// <summary>No longer operational after this local day; null = no closing planned (Opera: End Date).</summary>
    public DateOnly? ClosesOn { get; private set; }

    public bool IsActive { get; private set; }

    // The hotel's own details, filled by its people in step 3; nullable now so the table is made once.
    public string? ShortName { get; private set; }

    public string? UniqueName { get; private set; }

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public string? BusinessId { get; private set; }

    public string? BusinessRegistrationCode { get; private set; }

    public string? BusinessPremiseId { get; private set; }

    public int? FiscalYearBeginMonth { get; private set; }

    public int? FiscalYearBeginDay { get; private set; }

    public string? DefaultCulture { get; private set; }

    public int? StarRating { get; private set; }

    public string? Email { get; private set; }

    public string? Telephone { get; private set; }

    public string? WebsiteUrl { get; private set; }

    public string? ProductChampionEmail { get; private set; }

    public string? LegalOwnerName { get; private set; }

    public int? DisplayOrder { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    /// <summary>
    /// A new hotel of a customer, valid from the first line. The tenant is set here, explicitly: the context never
    /// stamps it. Raises PropertyCreatedDomainEvent, published by the framework after the save commits.
    /// </summary>
    public static Property Create(
        Guid tenantId,
        string code,
        string nameEn,
        string nameAr,
        string propertyTypeCode,
        Location location,
        PostalAddress address,
        string defaultCurrency,
        string defaultLanguageCode,
        int totalRooms,
        RegulatoryProfile regulatory,
        DateOnly? opensOn,
        DateOnly? closesOn,
        string? externalIdentifier)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(regulatory);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant is required.", nameof(tenantId));
        }

        if (totalRooms < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRooms), totalRooms, "A hotel has at least one room.");
        }

        if (closesOn is { } closes && opensOn is { } opens && closes < opens)
        {
            throw new ArgumentException("A hotel cannot close before it opens.", nameof(closesOn));
        }

        var property = new Property
        {
            TenantId = tenantId,
            Code = PropertyCode(code),
            NameEn = Name(nameEn, nameof(nameEn)),
            NameAr = Name(nameAr, nameof(nameAr)),
            PropertyTypeCode = ReferenceText.Key(propertyTypeCode, PropertyTypeRef.CodeMaxLength, nameof(propertyTypeCode)),
            Location = location,
            Address = address,
            DefaultCurrency = ReferenceText.Letters(defaultCurrency, 3, upper: true, nameof(defaultCurrency)),
            DefaultLanguageCode = ReferenceText.Letters(defaultLanguageCode, 2, upper: false, nameof(defaultLanguageCode)),
            TotalRooms = totalRooms,
            Regulatory = regulatory,
            OpensOn = opensOn,
            ClosesOn = closesOn,
            ExternalIdentifier = Optional(externalIdentifier, ExternalIdentifierMaxLength, nameof(externalIdentifier)),
            IsActive = true,
        };

        property.Raise(new PropertyCreatedDomainEvent(property));
        return property;
    }

    /// <summary>
    /// What the hotel may change about itself: names, address, dates and the rooms it really has. Never the code, the
    /// place, the money, the language or the regimes. The licence gap is the handler's to flag, never a refusal.
    /// </summary>
    public void Update(string nameEn, string nameAr, PostalAddress address, DateOnly? opensOn, DateOnly? closesOn, int totalRooms)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (totalRooms < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRooms), totalRooms, "A hotel has at least one room.");
        }

        if (closesOn is { } closes && opensOn is { } opens && closes < opens)
        {
            throw new ArgumentException("A hotel cannot close before it opens.", nameof(closesOn));
        }

        NameEn = Name(nameEn, nameof(nameEn));
        NameAr = Name(nameAr, nameof(nameAr));
        Address = address;
        OpensOn = opensOn;
        ClosesOn = closesOn;
        TotalRooms = totalRooms;
    }

    /// <summary>The hotel's own details, replaced whole: an empty value clears the column. Pairs travel together: both coordinates, both fiscal-year parts.</summary>
    public void UpdateDetails(
        string? shortName,
        string? uniqueName,
        decimal? latitude,
        decimal? longitude,
        string? businessId,
        string? businessRegistrationCode,
        string? businessPremiseId,
        int? fiscalYearBeginMonth,
        int? fiscalYearBeginDay,
        string? defaultCulture,
        int? starRating,
        string? email,
        string? telephone,
        string? websiteUrl,
        string? productChampionEmail,
        string? legalOwnerName,
        int? displayOrder)
    {
        if (latitude.HasValue != longitude.HasValue || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new ArgumentException("Coordinates travel together: latitude -90 to 90, longitude -180 to 180.", nameof(latitude));
        }

        // 2025 is not a leap year: a fiscal year never begins on 29 February.
        if (fiscalYearBeginMonth.HasValue != fiscalYearBeginDay.HasValue
            || fiscalYearBeginMonth is < 1 or > 12
            || (fiscalYearBeginMonth is { } month && fiscalYearBeginDay is { } day && (day < 1 || day > DateTime.DaysInMonth(2025, month))))
        {
            throw new ArgumentException("A fiscal year begins on a real day of a month, or is not set.", nameof(fiscalYearBeginDay));
        }

        if (starRating is < 1 or > StarRatingMax)
        {
            throw new ArgumentOutOfRangeException(nameof(starRating), starRating, $"Stars run 1 to {StarRatingMax}.");
        }

        if (displayOrder is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayOrder), displayOrder, "Display order is zero or more.");
        }

        ShortName = Optional(shortName, ShortNameMaxLength, nameof(shortName));
        UniqueName = Optional(uniqueName, UniqueNameMaxLength, nameof(uniqueName));
        Latitude = latitude;
        Longitude = longitude;
        BusinessId = Optional(businessId, BusinessTextMaxLength, nameof(businessId));
        BusinessRegistrationCode = Optional(businessRegistrationCode, BusinessTextMaxLength, nameof(businessRegistrationCode));
        BusinessPremiseId = Optional(businessPremiseId, BusinessTextMaxLength, nameof(businessPremiseId));
        FiscalYearBeginMonth = fiscalYearBeginMonth;
        FiscalYearBeginDay = fiscalYearBeginDay;
        DefaultCulture = Optional(defaultCulture, DefaultCultureMaxLength, nameof(defaultCulture));
        StarRating = starRating;
        Email = Optional(email, EmailMaxLength, nameof(email));
        Telephone = Optional(telephone, TelephoneMaxLength, nameof(telephone));
        WebsiteUrl = Optional(websiteUrl, WebsiteUrlMaxLength, nameof(websiteUrl));
        ProductChampionEmail = Optional(productChampionEmail, EmailMaxLength, nameof(productChampionEmail));
        LegalOwnerName = Optional(legalOwnerName, LegalOwnerNameMaxLength, nameof(legalOwnerName));
        DisplayOrder = displayOrder;
    }

    /// <summary>The hotel's own calendar day for a moment in UTC: Amman opens at Amman's midnight, not at UTC's.</summary>
    public DateOnly TodayAt(DateTimeOffset utcNow)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(Location.TimeZoneId)).DateTime);

    /// <summary>Open for business on this local day: active, opened, not yet closed.</summary>
    public bool IsOpenOn(DateOnly localDay)
        => IsActive && (OpensOn is null || localDay >= OpensOn) && (ClosesOn is null || localDay <= ClosesOn);

    private static string PropertyCode(string? value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!CodePattern().IsMatch(code))
        {
            throw new ArgumentException($"'{value}' is not a property code: 2 to {CodeMaxLength} characters of letters, digits, . - _", nameof(value));
        }

        return code;
    }

    private static string Name(string? value, string argument)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is 0 or > NameMaxLength)
        {
            throw new ArgumentException($"A name of 1 to {NameMaxLength} characters is required.", argument);
        }

        return name;
    }

    private static string? Optional(string? value, int maxLength, string argument)
    {
        var text = IdentityText.Blank(value);
        if (text is { Length: var length } && length > maxLength)
        {
            throw new ArgumentException($"At most {maxLength} characters.", argument);
        }

        return text;
    }

    [GeneratedRegex("^[A-Z0-9._-]{2,32}$")]
    private static partial Regex CodePattern();
}
