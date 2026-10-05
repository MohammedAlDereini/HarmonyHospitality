using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>
/// A city knows its country and its time zone, so choosing it fills both,
/// and a hotel in Jordan can no longer be saved with a Riyadh clock.
/// </summary>
public sealed class City : ReferenceEntity
{
    private City()
    {
    }

    public Guid Id { get; private set; }

    public string CountryCode { get; private set; } = null!;

    public string? SubdivisionCode { get; private set; }

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public string TimeZoneId { get; private set; } = null!;

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public bool IsActive { get; private set; }

    // The id is derived from country + English name at creation, so two installations that add Amman give it the same id.
    // The id then stays, whatever is renamed later: other tables point at the id, never at the name.
    public static City Create(string countryCode, string? subdivisionCode, string nameEn, string nameAr, string timeZoneId, decimal? latitude, decimal? longitude)
    {
        var country = ReferenceText.Letters(countryCode, 2, upper: true, nameof(countryCode));
        var name = ReferenceText.Name(nameEn, 128, nameof(nameEn));

        return new City
        {
            Id = DeterministicGuid.From("City", country, name.ToUpperInvariant()),
            CountryCode = country,
            SubdivisionCode = Subdivision(country, subdivisionCode),
            NameEn = name,
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            TimeZoneId = ReferenceText.Key(timeZoneId, 64, nameof(timeZoneId)),
            Latitude = Latitude90(latitude),
            Longitude = Longitude180(longitude),
            IsActive = true,
        };
    }

    /// <summary>Everything but the country can change; the country is in the id.</summary>
    public void Update(string nameEn, string nameAr, string? subdivisionCode, string timeZoneId, decimal? latitude, decimal? longitude)
    {
        NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn));
        NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr));
        SubdivisionCode = Subdivision(CountryCode, subdivisionCode);
        TimeZoneId = ReferenceText.Key(timeZoneId, 64, nameof(timeZoneId));
        Latitude = Latitude90(latitude);
        Longitude = Longitude180(longitude);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    private static string? Subdivision(string country, string? subdivisionCode)
    {
        var subdivision = string.IsNullOrWhiteSpace(subdivisionCode) ? null : subdivisionCode.Trim().ToUpperInvariant();

        if (subdivision is not null && !subdivision.StartsWith(country + "-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{subdivisionCode}' is not a subdivision of {country}.", nameof(subdivisionCode));
        }

        return subdivision;
    }

    private static decimal? Latitude90(decimal? latitude)
    {
        if (latitude is < -90m or > 90m)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        return latitude;
    }

    private static decimal? Longitude180(decimal? longitude)
    {
        if (longitude is < -180m or > 180m)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        return longitude;
    }
}
