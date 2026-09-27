namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>Hotel, Hostel, Resort... A row, so a new type needs no migration.</summary>
public sealed class PropertyTypeRef : ReferenceEntity
{
    private PropertyTypeRef()
    {
    }

    public string Code { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public static PropertyTypeRef Create(string code, string nameEn, string nameAr, int displayOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);

        return new PropertyTypeRef
        {
            Code = ReferenceText.Key(code, 32, nameof(code)),
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            DisplayOrder = displayOrder,
            IsActive = true,
        };
    }
}