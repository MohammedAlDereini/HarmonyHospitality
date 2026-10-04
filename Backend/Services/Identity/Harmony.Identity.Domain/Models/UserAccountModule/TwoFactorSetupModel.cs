namespace Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>What the person types or scans into the authenticator app. Shown during setup only; never stored in clear.</summary>
public class TwoFactorSetupModel
{
    /// <summary>The base32 secret, for manual entry.</summary>
    public string SharedKey { get; set; } = null!;

    /// <summary>The otpauth:// URI the client renders as a QR code.</summary>
    public string AuthenticatorUri { get; set; } = null!;
}
