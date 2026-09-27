namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>ISO 639-1.</summary>
public sealed class Language : ReferenceEntity
{
    private Language()
    {
    }

    public string Code { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string NameNative { get; private set; } = null!;

    public bool IsRightToLeft { get; private set; }

    public bool IsActive { get; private set; }

    public static Language Create(string code, string nameEn, string nameNative, bool isRightToLeft)
    {
        return new Language
        {
            Code = ReferenceText.Letters(code, 2, upper: false, nameof(code)),
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameNative = ReferenceText.Name(nameNative, 128, nameof(nameNative)),
            IsRightToLeft = isRightToLeft,
            IsActive = true,
        };
    }
}