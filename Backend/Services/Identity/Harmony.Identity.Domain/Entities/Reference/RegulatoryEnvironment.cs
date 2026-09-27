namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>
/// One table for all four environment codes. Identity owns the table so the foreign keys are real;
/// the Fiscal service owns the contents and fills it, so nothing is seeded here.
/// </summary>
public sealed class RegulatoryEnvironment : ReferenceEntity
{
    private RegulatoryEnvironment()
    {
    }

    public string Code { get; private set; } = null!;

    public RegulatoryKind Kind { get; private set; }

    public string CountryCode { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public string OwnerService { get; private set; } = null!;

    public static RegulatoryEnvironment Create(string code, RegulatoryKind kind, string countryCode, string nameEn, string nameAr, string ownerService)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new RegulatoryEnvironment
        {
            Code = ReferenceText.Key(code, 32, nameof(code)),
            Kind = kind,
            CountryCode = ReferenceText.Letters(countryCode, 2, upper: true, nameof(countryCode)),
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            OwnerService = ReferenceText.Name(ownerService, 64, nameof(ownerService)),
        };
    }
}