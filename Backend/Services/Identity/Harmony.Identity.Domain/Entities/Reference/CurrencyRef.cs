using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>ISO 4217. Named CurrencyRef because Currency is the framework's money type.</summary>
public sealed class CurrencyRef : ReferenceEntity
{
    private CurrencyRef()
    {
    }

    public string Code { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public int MinorUnits { get; private set; }

    public bool IsActive { get; private set; }

    // Minor units come from the framework's Currency registry, the one place they are defined,
    // so this table can never disagree with how Money rounds.
    public static CurrencyRef Create(string code, string nameEn, string nameAr)
    {
        var currency = Currency.Get(ReferenceText.Letters(code, 3, upper: true, nameof(code)));

        return new CurrencyRef
        {
            Code = currency.Code,
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            MinorUnits = currency.MinorUnitDigits,
            IsActive = true,
        };
    }
}