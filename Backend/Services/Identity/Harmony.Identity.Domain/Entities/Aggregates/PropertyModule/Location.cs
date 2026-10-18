using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;

/// <summary>
/// Where the hotel stands on the map: the city, and what the city decides for it (country, subdivision, time zone).
/// Built from a City row, never typed field by field: a hotel in Amman cannot be in Riyadh's time zone. Fixed once
/// saved, the way Mews fixes a property's time zone at creation.
/// </summary>
public sealed record Location
{
    private Location(Guid cityId, string countryCode, string? subdivisionCode, string timeZoneId)
    {
        CityId = cityId;
        CountryCode = countryCode;
        SubdivisionCode = subdivisionCode;
        TimeZoneId = timeZoneId;
    }

    public Guid CityId { get; }

    public string CountryCode { get; }

    public string? SubdivisionCode { get; }

    /// <summary>IANA id, the city's.</summary>
    public string TimeZoneId { get; }

    /// <summary>The only way to make one: from an active city row.</summary>
    public static Location Of(City city)
    {
        ArgumentNullException.ThrowIfNull(city);
        if (!city.IsActive)
        {
            throw new ArgumentException($"City {city.Id} is inactive.", nameof(city));
        }

        return new Location(city.Id, city.CountryCode, city.SubdivisionCode, city.TimeZoneId);
    }
}
