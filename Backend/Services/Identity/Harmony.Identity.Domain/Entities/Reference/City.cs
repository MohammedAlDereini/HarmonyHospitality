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

    // The id is derived from country + English name, so every installation gives Amman the same id
    // and data moves between environments without remapping.
    public static City Create(string countryCode, string? subdivisionCode, string nameEn, string nameAr, string timeZoneId, decimal? latitude, decimal? longitude)
    {
        var country = ReferenceText.Letters(countryCode, 2, upper: true, nameof(countryCode));
        var subdivision = string.IsNullOrWhiteSpace(subdivisionCode) ? null : subdivisionCode.Trim().ToUpperInvariant();
        var name = ReferenceText.Name(nameEn, 128, nameof(nameEn));

        if (subdivision is not null && !subdivision.StartsWith(country + "-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{subdivisionCode}' is not a subdivision of {country}.", nameof(subdivisionCode));
        }

        if (latitude is < -90m or > 90m)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        if (longitude is < -180m or > 180m)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        return new City
        {
            Id = DeterministicGuid.From("City", country, name.ToUpperInvariant()),
            CountryCode = country,
            SubdivisionCode = subdivision,
            NameEn = name,
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            TimeZoneId = ReferenceText.Key(timeZoneId, 64, nameof(timeZoneId)),
            Latitude = latitude,
            Longitude = longitude,
            IsActive = true,
        };
    }
}