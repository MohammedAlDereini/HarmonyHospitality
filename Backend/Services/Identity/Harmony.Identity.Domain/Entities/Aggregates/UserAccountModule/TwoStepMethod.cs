namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>How a person proves the second step at sign-in, once two-step sign-in is on (Identity's TwoFactorEnabled).</summary>
public enum TwoStepMethod
{
    /// <summary>The 6-digit code from an authenticator app (Microsoft or Google Authenticator).</summary>
    AuthenticatorApp = 1,

    /// <summary>A one-time link sent to the account's e-mail, opened on the same computer (like Mews).</summary>
    EmailLink = 2,
}
