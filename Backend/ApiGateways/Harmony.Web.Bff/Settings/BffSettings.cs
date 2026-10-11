namespace Harmony.Web.Bff.Settings;

/// <summary>
/// BffSettings: everything this host needs, checked at startup — a wrong value refuses to start, never at 03:00.
/// The BFF is the confidential client of RFC 10017 §6.1: the browser holds ONE cookie, tokens never leave this host.
/// </summary>
public sealed class BffSettings
{
    public const string Section = "BffSettings";

    /// <summary>Identity's issuer, absolute https (e.g. https://localhost:5000).</summary>
    public string Authority { get; set; } = null!;

    /// <summary>The registered confidential client. Never a secret string: the key below signs a private_key_jwt.</summary>
    public string ClientId { get; set; } = "harmony-web-bff";

    /// <summary>PEM file holding the EC P-256 PRIVATE key this host signs client assertions with.</summary>
    public string ClientPrivateKeyPath { get; set; } = null!;

    public string[] Scopes { get; set; } = ["openid", "profile", "harmony.profile", "harmony", "offline_access"];

    /// <summary>The session cookie. __Host- is load-bearing: the browser itself enforces Secure + Path=/ + no Domain.</summary>
    public string CookieName { get; set; } = "__Host-harmony-session";

    /// <summary>
    /// Idle minutes before the session ends, 5 to 720. The default mirrors Identity's session rule (60 minutes
    /// idle — NIST 800-63B rev 4 AAL2's ceiling, enforced there by the refresh token): a cookie that pretended
    /// to live longer would die at the refresh anyway — the clocks agree on purpose. A card-strict deployment
    /// (PCI DSS 8.2.8 reads 15 minutes) dials this down here, no code change.
    /// </summary>
    public int SessionIdleMinutes { get; set; } = 60;

    /// <summary>The hard cap in hours regardless of activity, 1 to 24: Identity's absolute session rule (12 hours, half of AAL2's 24-hour ceiling).</summary>
    public int SessionAbsoluteHours { get; set; } = 12;

    /// <summary>Where /api/{**} is forwarded (step 3). Absolute https, origin only.</summary>
    public string ApiDestination { get; set; } = null!;

    /// <summary>Serve the built web app from wwwroot (production). Off in development, where the dev server serves it.</summary>
    public bool ServeStaticFiles { get; set; }

    /// <summary>Front doors whose X-Forwarded-For is believed. EMPTY MEANS TRUST NOBODY — the safe default.</summary>
    public string[] TrustedProxyAddresses { get; set; } = [];

    /// <summary>
    /// Where a password manager sends a person to add a passkey, and to manage them (the W3C "Passkey Endpoints Well-Known
    /// URL", served here at /.well-known/passkey-endpoints). Both absolute https, or both empty: the document is then not
    /// served. The front door routes that path of the relying-party domain's apex (https://harmony.local/...) to this host.
    /// </summary>
    public string? PasskeyEnrollUrl { get; set; }

    public string? PasskeyManageUrl { get; set; }

    public bool ServesPasskeyEndpoints => !string.IsNullOrWhiteSpace(this.PasskeyEnrollUrl) && !string.IsNullOrWhiteSpace(this.PasskeyManageUrl);

    public static bool IsValid(BffSettings settings)
        => Uri.TryCreate(settings.Authority, UriKind.Absolute, out var authority)
            && authority.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrWhiteSpace(settings.ClientId)
            && !string.IsNullOrWhiteSpace(settings.ClientPrivateKeyPath)
            && File.Exists(settings.ClientPrivateKeyPath)
            && settings.Scopes.Length > 0
            && settings.CookieName.StartsWith("__Host-", StringComparison.Ordinal)
            && settings.SessionIdleMinutes is >= 5 and <= 720
            && settings.SessionAbsoluteHours is >= 1 and <= 24
            && Uri.TryCreate(settings.ApiDestination, UriKind.Absolute, out var api)
            && api.Scheme == Uri.UriSchemeHttps
            && PasskeyEndpointsValid(settings);

    private static bool PasskeyEndpointsValid(BffSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.PasskeyEnrollUrl) && string.IsNullOrWhiteSpace(settings.PasskeyManageUrl))
        {
            return true;
        }

        return Uri.TryCreate(settings.PasskeyEnrollUrl, UriKind.Absolute, out var enroll) && enroll.Scheme == Uri.UriSchemeHttps
            && Uri.TryCreate(settings.PasskeyManageUrl, UriKind.Absolute, out var manage) && manage.Scheme == Uri.UriSchemeHttps;
    }
}
