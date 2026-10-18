namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>
/// Something a customer subscribes to: per hotel (reservations, POS) or once for the whole customer (loyalty,
/// multi-property), which <see cref="Level"/> says. Mews and Opera both call these modules. The code is what a service
/// checks a subscription line against, so it never changes.
/// </summary>
public sealed class Module : ReferenceEntity
{
    /// <summary>The width of the Code column; subscription lines and the door rules read it from here.</summary>
    public const int CodeMaxLength = 64;

    private Module()
    {
    }

    public string Code { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string OwnerService { get; private set; } = null!;

    /// <summary>Property: a line of this module names a hotel. Tenant: it names none and covers the whole customer.</summary>
    public ModuleLevel Level { get; private set; }

    public bool HasLimit { get; private set; }

    public static Module Create(string code, string nameEn, string ownerService, ModuleLevel level, bool hasLimit)
    {
        return new Module
        {
            Code = ReferenceText.Key(code, CodeMaxLength, nameof(code)),
            NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn)),
            OwnerService = ReferenceText.Name(ownerService, 64, nameof(ownerService)),
            Level = LevelOf(level),
            HasLimit = hasLimit,
        };
    }

    public void Update(string nameEn, string ownerService, ModuleLevel level, bool hasLimit)
    {
        NameEn = ReferenceText.Name(nameEn, 128, nameof(nameEn));
        OwnerService = ReferenceText.Name(ownerService, 64, nameof(ownerService));
        Level = LevelOf(level);
        HasLimit = hasLimit;
    }

    private static ModuleLevel LevelOf(ModuleLevel level)
        => Enum.IsDefined(level) ? level : throw new ArgumentOutOfRangeException(nameof(level), level, "The level is Property or Tenant.");
}
