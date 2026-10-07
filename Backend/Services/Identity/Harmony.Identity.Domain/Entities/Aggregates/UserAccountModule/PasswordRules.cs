namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// The one number every password check agrees on: Identity's password options, the first-account seed and the
/// change-password validation all read it here, so the rule cannot drift between them. 12 with at least one symbol,
/// like Mews (the symbol is Identity's RequireNonAlphanumeric; a space counts).
/// </summary>
public static class PasswordRules
{
    public const int MinimumLength = 12;

    /// <summary>The words no password may contain, whoever it belongs to: the name of the service (NIST SP 800-63B-4 blocklist).</summary>
    public static readonly IReadOnlyList<string> ServiceWords = ["harmony"];

    /// <summary>Identity's error code when a password appears in a known data breach.</summary>
    public const string LeakedErrorCode = "PasswordLeaked";

    /// <summary>Identity's error code when a password contains the service name or the person's own name or e-mail name.</summary>
    public const string ContextWordErrorCode = "PasswordContainsContextWord";
}
