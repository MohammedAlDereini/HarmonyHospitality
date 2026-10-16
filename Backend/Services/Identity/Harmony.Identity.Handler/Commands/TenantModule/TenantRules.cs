namespace Harmony.Identity.Handler.Commands.TenantModule;

/// <summary>The input formats the tenant validators share; they mirror what the entity accepts.</summary>
public static class TenantRules
{
    /// <summary>2 to 32 characters: letters, digits, . - _ (stored upper-case).</summary>
    public const string CodePattern = "^[A-Za-z0-9._-]{2,32}$";

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public const string CountryCodePattern = "^[A-Za-z]{2}$";

    /// <summary>ISO 4217.</summary>
    public const string CurrencyCodePattern = "^[A-Za-z]{3}$";
}
