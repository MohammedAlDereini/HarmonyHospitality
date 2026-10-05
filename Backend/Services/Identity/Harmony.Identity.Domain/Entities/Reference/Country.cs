namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>ISO 3166-1. Kept by the platform admin through the Reference API; code and alpha-3 are its identity and never change.</summary>
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
        return new Country
        {
            Code = ReferenceText.Letters(code, 2, upper: true, nameof(code)),
            Alpha3 = ReferenceText.Letters(alpha3, 3, upper: true, nameof(alpha3)),
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            DialCode = Dial(dialCode),
            DefaultCurrency = ReferenceText.Letters(defaultCurrency, 3, upper: true, nameof(defaultCurrency)),
            IsActive = true,
        };
    }

    public void Update(string nameEn, string nameAr, string dialCode, string defaultCurrency)
    {
        NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn));
        NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr));
        DialCode = Dial(dialCode);
        DefaultCurrency = ReferenceText.Letters(defaultCurrency, 3, upper: true, nameof(defaultCurrency));
    }

    /// <summary>An inactive country is kept (rows point at it) but offered to nobody new.</summary>
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    private static string Dial(string? dialCode)
    {
        var dial = dialCode?.Trim() ?? string.Empty;

        if (dial.Length < 2 || dial.Length > 5 || dial[0] != '+' || !dial.Skip(1).All(char.IsAsciiDigit))
        {
            throw new ArgumentException($"'{dialCode}' is not a dial code like +962.", nameof(dialCode));
        }

        return dial;
    }
}
