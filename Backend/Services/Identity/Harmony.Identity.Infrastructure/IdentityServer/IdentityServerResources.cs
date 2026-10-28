using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Harmony.Core.Identity.Implementations.Platform;
using Harmony.Identity.Infrastructure.IdentityServer.Yarp;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// What Duende may issue tokens for. In code, not in a database: one platform audience, a handful of clients.
/// 🔴 SERVICES: the machine client lived here; decided at the services step.
/// </summary>
public static class IdentityServerResources
{
    /// <summary>The platform scope and audience: every service validates "aud: harmony" (IdentitySettings:Platform:ValidAudiences).</summary>
    public const string PlatformScope = "harmony";

    /// <summary>The claims a Harmony token carries about its subject. Issuance supplies them; this list lets them through.</summary>
    private static readonly string[] UserClaims =
    [
        PlatformClaimTypes.Tenant,
        PlatformClaimTypes.Role,
        "name",
        PlatformClaimTypes.SecurityVersion,
        PlatformClaimTypes.MustChangePassword,
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope(PlatformScope, "Harmony platform"),
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource(PlatformScope, "Harmony platform")
        {
            Scopes = { PlatformScope },
            UserClaims = UserClaims,
        },
    ];

    /// <summary>The identity scope the BFF asks for so ITS OWN session knows who the person is to Harmony.</summary>
    public const string UserProfileScope = "harmony.profile";

    /// <summary>OpenID Connect sign-in (sub) and the display name (profile → name), for the BFF's own session.</summary>
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),

        // The profile service filters to REQUESTED claim types (AddRequestedClaims): without this scope, userinfo
        // would answer the BFF with a name and nothing else — no roles to draw menus with, no tenant.
        new IdentityResource(UserProfileScope, "Who you are to Harmony", [PlatformClaimTypes.Role, PlatformClaimTypes.Tenant]),
    ];

    /// <summary>15 minutes: a token is short-lived; a demoted or terminated user is stopped sooner by security_version anyway.</summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The session rule: 60 minutes idle, 12 hours absolute, enforced by the refresh token. 60 is NIST 800-63B
    /// rev 4 AAL2's idle ceiling exactly; 12 is half its 24-hour absolute ceiling. Opera hard-codes 15 (the PCI
    /// DSS 8.2.8 letter), Mews never signs anyone out, Microsoft defaults to 24-hour idle with no absolute cap —
    /// we sit between, and a card-strict deployment tightens the BFF's own knob without touching this.
    /// One constant drives the BFF client's refresh window AND Duende's own sign-in cookie.
    /// </summary>
    public static readonly TimeSpan RefreshIdleLifetime = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan RefreshAbsoluteLifetime = TimeSpan.FromHours(12);

    /// <summary>
    /// A pushed sign-in request must outlive the whole sign-in: typing the password, the two-step code, setting up two-step.
    /// 10 minutes is Duende's default and the FAPI 2.0 maximum; shorter fails people who are slow to type.
    /// </summary>
    public static readonly TimeSpan PushedAuthorizationLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Set on a client whose people are the platform's own (Entra calls this "assignment required" on an application):
    /// a hotel account is refused there, on the sign-in page and at every token. Duende's Client.Properties is the place
    /// for a client-specific rule like this one.
    /// </summary>
    public const string PlatformOnlyProperty = "harmony:platform-only";

    public static bool IsPlatformOnly(Client client)
        => client.Properties.TryGetValue(PlatformOnlyProperty, out var value) && value == "true";

    /// <param name="bffOrigins">The web BFF's origins (IdentitySettings:WebBff:Origins).</param>
    /// <param name="bffPublicKeys">The web BFF's public keys as JSON Web Keys (current first, then those kept during a rotation).</param>
    /// <param name="consoleOrigins">The console BFF's origins (IdentitySettings:ConsoleBff:Origins); empty = no console client yet.</param>
    /// <param name="consolePublicKeys">The console BFF's public keys as JSON Web Keys.</param>
    public static IEnumerable<Client> Clients(
        IReadOnlyCollection<string> bffOrigins,
        IEnumerable<string> bffPublicKeys,
        IReadOnlyCollection<string> consoleOrigins,
        IEnumerable<string> consolePublicKeys)
    {
        // The ONE way a person signs in (BFF step 5 removed the web app's password grant): RFC 10017 §6.1, the BFF as a
        // confidential client using the authorization code grant with PKCE; the tokens stay on it, no browser ever
        // calls the token endpoint.
        yield return BffClient(WebBffSettings.ClientId, "Harmony web (BFF)", bffOrigins, bffPublicKeys, platformOnly: false);

        // The console: the same BFF code on its own host, for the platform's own people only (B9).
        if (consoleOrigins.Count > 0)
        {
            yield return BffClient(ConsoleBffSettings.ClientId, "Harmony console (BFF)", consoleOrigins, consolePublicKeys, platformOnly: true);
        }
    }

    private static Client BffClient(string clientId, string clientName, IReadOnlyCollection<string> origins, IEnumerable<string> publicKeys, bool platformOnly) => new()
    {
        ClientId = clientId,
        ClientName = clientName,
        Properties = platformOnly ? new Dictionary<string, string> { [PlatformOnlyProperty] = "true" } : new Dictionary<string, string>(),

        // private_key_jwt (RFC 7523): the BFF signs, Identity checks with the public key. No shared secret exists.
        ClientSecrets = [.. publicKeys.Select(jwk => new Secret { Type = IdentityServerConstants.SecretTypes.JsonWebKey, Value = jwk })],
        RequireClientSecret = true,

        // DPoP (RFC 9449, later.md L3): every token for this client is bound to the key the BFF proves it holds; a
        // copy taken from a log, a proxy or a memory dump is refused by every API without that key. The proof's own
        // freshness is its iat (Duende's default mode, 5 minutes of skew); no server nonce round trip.
        RequireDPoP = true,

        AllowedGrantTypes = GrantTypes.Code,
        RequirePkce = true,
        AllowPlainTextPkce = false,

        // PAR (RFC 9126): the sign-in details are pushed server to server; the browser carries only a one-time reference.
        RequirePushedAuthorization = true,
        PushedAuthorizationLifetime = (int)PushedAuthorizationLifetime.TotalSeconds,

        RedirectUris = [.. origins.Select(origin => origin + BffClientSettings.RedirectPath)],
        PostLogoutRedirectUris = [.. origins.Select(origin => origin + BffClientSettings.PostLogoutRedirectPath)],

        // Ending a session here tells the BFF too — twice over: the best-effort iframe (front-channel) and
        // the reliable server-to-server POST (back-channel; suspend and sign-out-everywhere ride it, because
        // the session service removes sessions through Duende, which notifies by default). Never throw here
        // on an empty list: startup validation refuses it with the NAMED message.
        FrontChannelLogoutUri = origins.Count > 0 ? origins.First() + BffClientSettings.FrontChannelLogoutPath : null,
        BackChannelLogoutUri = origins.Count > 0 ? origins.First() + BffClientSettings.BackChannelLogoutPath : null,

        // A server-side client: no browser calls Identity's endpoints for it, so no CORS origin.
        AllowedCorsOrigins = [],

        AllowedScopes =
        {
            IdentityServerConstants.StandardScopes.OpenId,
            IdentityServerConstants.StandardScopes.Profile,
            UserProfileScope,
            PlatformScope,
            IdentityServerConstants.StandardScopes.OfflineAccess,
        },
        AllowOfflineAccess = true,
        RequireConsent = false,
        AlwaysIncludeUserClaimsInIdToken = false,

        AccessTokenLifetime = (int)AccessTokenLifetime.TotalSeconds,
        RefreshTokenUsage = TokenUsage.OneTimeOnly,
        RefreshTokenExpiration = TokenExpiration.Sliding,
        SlidingRefreshTokenLifetime = (int)RefreshIdleLifetime.TotalSeconds,
        AbsoluteRefreshTokenLifetime = (int)RefreshAbsoluteLifetime.TotalSeconds,
        UpdateAccessTokenClaimsOnRefresh = true,
    };
}
