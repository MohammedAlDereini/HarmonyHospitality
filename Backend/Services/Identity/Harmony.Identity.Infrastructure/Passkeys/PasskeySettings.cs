namespace Harmony.Identity.Infrastructure.Passkeys;

/// <summary>
/// Passkeys (WebAuthn). A passkey is bound to a domain, the "relying party id": the browser lets a site use a passkey
/// only when the site's host is that domain or sits under it. The web app (the BFF's origin) creates the passkeys and
/// the sign-in page (this service's host) uses them, so the domain must cover BOTH hosts — checked at start.
/// </summary>
public class PasskeySettings
{
    public const string Section = "IdentitySettings:Passkeys";

    /// <summary>
    /// The domain the passkeys belong to: "harmony.local" for app.harmony.local and identity.harmony.local. Empty means
    /// the issuer's own host, which is only right when the web app lives on that same host (localhost in Development).
    /// Never a public suffix like "com" or "co.uk": browsers refuse those.
    /// </summary>
    public string? ServerDomain { get; set; }

    /// <summary>The domain in force: the setting, or the issuer's host.</summary>
    public static string EffectiveDomain(PasskeySettings settings, string issuer)
        => Normalize(settings.ServerDomain) ?? new Uri(issuer, UriKind.Absolute).Host.ToLowerInvariant();

    /// <summary>True when every host (the issuer's and each BFF origin's) is the domain or sits under it.</summary>
    public static bool IsValid(PasskeySettings settings, IEnumerable<string> bffOrigins, string issuer)
    {
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
        {
            return false;
        }

        var hosts = new List<string> { issuerUri.Host };
        foreach (var origin in bffOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
            {
                return false;
            }

            hosts.Add(originUri.Host);
        }

        var domain = Normalize(settings.ServerDomain) ?? issuerUri.Host.ToLowerInvariant();
        if (domain.Length == 0 || domain.StartsWith('.') || domain.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return hosts.All(host => host.Equals(domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('.').ToLowerInvariant();
}
