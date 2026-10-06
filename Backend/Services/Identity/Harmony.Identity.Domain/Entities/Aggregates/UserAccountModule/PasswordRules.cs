namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// The one number every password check agrees on: Identity's password options, the first-account seed and the
/// change-password validation all read it here, so the rule cannot drift between them. 12 with at least one symbol,
/// like Mews (the symbol is Identity's RequireNonAlphanumeric; a space counts).
/// </summary>
public static class PasswordRules
{
    public const int MinimumLength = 12;
}
