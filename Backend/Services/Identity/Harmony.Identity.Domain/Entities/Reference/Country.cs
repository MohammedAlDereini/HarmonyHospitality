namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>ISO 3166-1.</summary>
public sealed class Country : ReferenceEntity
{
    private Country()
    {
    }

    public string Code { get; private set; } = null!;

    public string Alpha3 { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public string DialCode { get; private set; } = null!;

    public string DefaultCurrency { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public static Country Create(string code, string alpha3, string nameEn, string nameAr, string dialCode, string defaultCurrency)
    {
        var dial = dialCode?.Trim() ?? string.Empty;

        if (dial.Length < 2 || dial.Length > 5 || dial[0] != '+' || !dial.Skip(1).All(char.IsAsciiDigit))
        {
            throw new ArgumentException($"'{dialCode}' is not a dial code like +962.", nameof(dialCode));
        }

        return new Country
        {
            Code = ReferenceText.Letters(code, 2, upper: true, nameof(code)),
            Alpha3 = ReferenceText.Letters(alpha3, 3, upper: true, nameof(alpha3)),
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            DialCode = dial,
            DefaultCurrency = ReferenceText.Letters(defaultCurrency, 3, upper: true, nameof(defaultCurrency)),
            IsActive = true,
        };
    }
}