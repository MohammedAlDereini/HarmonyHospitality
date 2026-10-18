namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>Hotel, Hostel, Resort... A row, so a new type needs no migration.</summary>
public sealed class PropertyTypeRef : ReferenceEntity
{
    /// <summary>The width of the Code column; the hotel's value objects and the door rules read it from here.</summary>
    public const int CodeMaxLength = 32;

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
            Code = ReferenceText.Key(code, CodeMaxLength, nameof(code)),
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr)),
            DisplayOrder = displayOrder,
            IsActive = true,
        };
    }

    public void Update(string nameEn, string nameAr, int displayOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);

        NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn));
        NameAr = ReferenceText.Name(nameAr, 128, nameof(nameAr));
        DisplayOrder = displayOrder;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
