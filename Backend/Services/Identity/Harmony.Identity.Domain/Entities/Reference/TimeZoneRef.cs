namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>IANA zone. Named TimeZoneRef because TimeZone is a BCL type.</summary>
public sealed class TimeZoneRef : ReferenceEntity
{
    private TimeZoneRef()
    {
    }

    public string Id { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public bool IsActive { get; private set; }

    // A zone the runtime cannot resolve becomes a property that cannot compute a business date,
    // so it is refused here rather than discovered at night audit.
    public static TimeZoneRef Create(string id, string nameEn, string nameAr)
    {
        var zone = ReferenceText.Key(id, 64, nameof(id));

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _))
        {
            throw new ArgumentException($"'{id}' is not a time zone this runtime can resolve.", nameof(id));
        }

        return new TimeZoneRef
        {
            Id = zone,
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            IsActive = true,
        };
    }
}