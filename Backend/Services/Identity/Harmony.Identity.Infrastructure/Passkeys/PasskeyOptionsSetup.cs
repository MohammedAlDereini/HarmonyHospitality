using Harmony.Identity.Infrastructure.IdentityServer.Yarp;
using Harmony.Identity.Infrastructure.Signing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Infrastructure.Passkeys;

/// <summary>
/// The rules Identity's passkey handler works by. The person must verify themselves on the device (biometric or PIN):
/// a passkey is then a complete sign-in, the key being one factor and the verification the other (NIST SP 800-63B-4
/// calls it a multi-factor cryptographic authenticator, AAL2). The passkey must be discoverable, so the sign-in page
/// finds it without asking for an e-mail first. And the browser's origin must be one of ours: the web app (where
/// passkeys are created) or this service (where they sign in); Identity's default would accept this host only. The
/// origin is compared part by part, scheme, host and port, never as text (OWASP: "scheme, host, and port").
/// </summary>
public sealed class PasskeyOptionsSetup : IConfigureOptions<IdentityPasskeyOptions>
{
    private readonly PasskeySettings settings;
    private readonly WebBffSettings bff;
    private readonly TokenIssuerSettings issuer;

    public PasskeyOptionsSetup(IOptions<PasskeySettings> settings, IOptions<WebBffSettings> bff, IOptions<TokenIssuerSettings> issuer)
    {
        this.settings = settings.Value;
        this.bff = bff.Value;
        this.issuer = issuer.Value;
    }

    public void Configure(IdentityPasskeyOptions options)
    {
        var allowed = new HashSet<(string Scheme, string Host, int Port)>();
        foreach (var origin in this.bff.Origins.Append(this.issuer.Issuer))
        {
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                allowed.Add(PartsOf(uri));
            }
        }

        options.ServerDomain = PasskeySettings.EffectiveDomain(this.settings, this.issuer.Issuer);
        options.UserVerificationRequirement = "required";
        options.ResidentKeyRequirement = "required";
        options.ValidateOrigin = context => ValueTask.FromResult(IsOurs(context, allowed));
    }

    /// <summary>
    /// Ours: not cross-origin, an absolute https URL, no user name in it (a browser never writes one), and its scheme,
    /// host and port each equal to one allowed origin's.
    /// </summary>
    internal static bool IsOurs(PasskeyOriginValidationContext context, HashSet<(string Scheme, string Host, int Port)> allowed)
    {
        if (context.CrossOrigin || !Uri.TryCreate(context.Origin, UriKind.Absolute, out var origin))
        {
            return false;
        }

        if (origin.Scheme != Uri.UriSchemeHttps || origin.UserInfo.Length > 0 || origin.PathAndQuery.Length > 1 || origin.Fragment.Length > 0)
        {
            return false;
        }

        return allowed.Contains(PartsOf(origin));
    }

    private static (string Scheme, string Host, int Port) PartsOf(Uri uri)
        => (uri.Scheme.ToLowerInvariant(), uri.Host.ToLowerInvariant(), uri.Port);
}
