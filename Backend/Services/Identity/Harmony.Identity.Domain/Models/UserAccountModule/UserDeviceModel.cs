namespace Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>A browser the account signed in from. Never the device key or its hash.</summary>
public class UserDeviceModel
{
    public Guid Id { get; set; }
    public string Label { get; set; } = null!;
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public string? LastAddress { get; set; }

    /// <summary>True while the sign-in session last opened on this device is still open.</summary>
    public bool SignedIn { get; set; }

    /// <summary>When the person trusted this browser; null: not trusted.</summary>
    public DateTimeOffset? TrustedAtUtc { get; set; }
}
