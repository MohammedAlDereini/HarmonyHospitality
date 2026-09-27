namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>ISO 3166-2: governorates, regions, emirates, states.</summary>
public sealed class CountrySubdivision : ReferenceEntity
{
    private CountrySubdivision()
    {
    }

    public string Code { get; private set; } = null!;

    public string CountryCode { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public string Kind { get; private set; } = null!;

    public static CountrySubdivision Create(string countryCode, string code, string nameEn, string nameAr, string kind)
    {
        var country = ReferenceText.Letters(countryCode, 2, upper: true, nameof(countryCode));
        var subdivision = code?.Trim().ToUpperInvariant() ?? string.Empty;
        var suffix = subdivision.StartsWith(country + "-", StringComparison.Ordinal) ? subdivision[3..] : string.Empty;

        if (suffix.Length is < 1 or > 3 || !suffix.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException($"'{code}' is not an ISO 3166-2 code of {country}, like {country}-AM.", nameof(code));
        }

        return new CountrySubdivision
        {
            Code = subdivision,
            CountryCode = country,
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            Kind = ReferenceText.Name(kind, 32, nameof(kind)),
        };
    }
}