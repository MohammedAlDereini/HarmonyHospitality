namespace Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>
/// A passkey of an account. Never the public key. The credential id is there for the browser only (WebAuthn's signal
/// methods, which tell a device which keys still exist); the web app never shows it.
/// </summary>
public class UserPasskeyModel
{
    public Guid Id { get; set; }

    /// <summary>The credential id, base64url, as the browser knows it.</summary>
    public string CredentialId { get; set; } = null!;

    /// <summary>The domain the passkey belongs to (the relying-party id), for the browser's signal methods.</summary>
    public string RpId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? LastUsedUtc { get; set; }

    /// <summary>True when the passkey is synced to the person's other devices (a password manager, Apple or Google account).</summary>
    public bool IsBackedUp { get; set; }

    /// <summary>True when the authenticator could sync it; false for a device-bound passkey such as a security key.</summary>
    public bool IsBackupEligible { get; set; }

    /// <summary>How the authenticator reaches the browser ("internal", "hybrid", "usb"...), as it reported at creation.</summary>
    public string[] Transports { get; set; } = [];
}
