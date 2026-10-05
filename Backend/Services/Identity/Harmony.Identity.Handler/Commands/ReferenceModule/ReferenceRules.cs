namespace Harmony.Identity.Handler.Commands.ReferenceModule;

/// <summary>
/// The input formats the reference validators share. They mirror what the entities accept, minus the slash in a code,
/// because a code travels in a URL (GET api/Country/{code}). A time zone id keeps its slash and uses a catch-all route.
/// </summary>
public static class ReferenceRules
{
    /// <summary>2 to 32 characters: letters, digits, . - _ (property types, group types, regulatory environments).</summary>
    public const string CodePattern = "^[A-Za-z0-9._-]{2,32}$";

    /// <summary>2 to 64 characters: letters, digits, . - _ (capabilities, like pms.core).</summary>
    public const string CapabilityCodePattern = "^[A-Za-z0-9._-]{2,64}$";

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public const string CountryCodePattern = "^[A-Za-z]{2}$";

    /// <summary>ISO 3166-1 alpha-3.</summary>
    public const string Alpha3Pattern = "^[A-Za-z]{3}$";

    /// <summary>ISO 4217.</summary>
    public const string CurrencyCodePattern = "^[A-Za-z]{3}$";

    /// <summary>ISO 639-1.</summary>
    public const string LanguageCodePattern = "^[A-Za-z]{2}$";

    /// <summary>A plus sign and 1 to 4 digits, like +962.</summary>
    public const string DialCodePattern = @"^\+[0-9]{1,4}$";

    /// <summary>ISO 3166-2: the country, a hyphen, 1 to 3 letters or digits, like JO-AM.</summary>
    public const string SubdivisionCodePattern = "^[A-Za-z]{2}-[A-Za-z0-9]{1,3}$";

    /// <summary>IANA zone id, like Asia/Amman: 2 to 64 characters of letters, digits, . - _ /</summary>
    public const string TimeZoneIdPattern = "^[A-Za-z0-9._/-]{2,64}$";
}
