namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>
/// Brand, Region, Owner, Custom. IsExclusive is what the one-brand-per-hotel rule reads,
/// so the word Brand is never hardcoded in a constraint. A system row is one that code relies on:
/// it can be renamed, but its exclusivity is fixed.
/// </summary>
public sealed class PropertyGroupTypeRef : ReferenceEntity
{
    public const int CodeMaxLength = 32;

    /// <summary>The one code the schema names: the BrandDetails CHECK and the one-brand-per-hotel index. Every other rule reads IsExclusive.</summary>
    public const string BrandCode = "Brand";

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
            Code = ReferenceText.Key(code, CodeMaxLength, nameof(code)),
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            IsExclusive = isExclusive,
            IsSystem = isSystem,
        };
    }

    public void Update(string nameEn, string nameAr, bool isExclusive)
    {
        if (IsSystem && isExclusive != IsExclusive)
        {
            throw new InvalidOperationException($"'{Code}' is a system group type; its exclusivity cannot change.");
        }

        NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn));
        NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr));
        IsExclusive = isExclusive;
    }
}
