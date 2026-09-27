namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>
/// Brand, Region, Owner, Custom. IsExclusive is what the one-brand-per-hotel rule reads,
/// so the word Brand is never hardcoded in a constraint.
/// </summary>
public sealed class PropertyGroupTypeRef : ReferenceEntity
{
    private PropertyGroupTypeRef()
    {
    }

    public string Code { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public bool IsExclusive { get; private set; }

    public bool IsSystem { get; private set; }

    public static PropertyGroupTypeRef Create(string code, string nameEn, string nameAr, bool isExclusive, bool isSystem)
    {
        return new PropertyGroupTypeRef
        {
            Code = ReferenceText.Key(code, 32, nameof(code)),
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            IsExclusive = isExclusive,
            IsSystem = isSystem,
        };
    }
}