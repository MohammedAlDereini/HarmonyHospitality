namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>Something a tenant can buy. Services register their own, as they register settings.</summary>
public sealed class Capability : ReferenceEntity
{
    private Capability()
    {
    }

    public string Code { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string OwnerService { get; private set; } = null!;

    public bool HasLimit { get; private set; }

    public static Capability Create(string code, string nameEn, string ownerService, bool hasLimit)
    {
        return new Capability
        {
            Code = ReferenceText.Key(code, 64, nameof(code)),
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            OwnerService = ReferenceText.Name(ownerService, 64, nameof(ownerService)),
            HasLimit = hasLimit,
        };
    }
}