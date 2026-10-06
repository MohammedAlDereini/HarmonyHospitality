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
        PlatformClaimTypes.Org,
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

    /// <summary>OpenID Connect sign-in (sub) and the display name (profile → name), for the BFF's own session.</summary>
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
    ];

    /// <summary>The web client people sign in with today: PropX email + password, issued by Duende as the password grant.</summary>
    public const string WebClientId = "harmony-web";

    /// <summary>15 minutes: a token is short-lived; a demoted or terminated user is stopped sooner by security_version anyway.</summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>The design docs session rule: 30 minutes idle, 12 hours absolute, enforced by the refresh token.</summary>
    public static readonly TimeSpan RefreshIdleLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan RefreshAbsoluteLifetime = TimeSpan.FromHours(12);

    /// <summary>
    /// A pushed sign-in request must outlive the whole sign-in: typing the password, the two-step code, setting up two-step.
    /// 10 minutes is Duende's default and the FAPI 2.0 maximum; shorter fails people who are slow to type.
    /// </summary>
    public static readonly TimeSpan PushedAuthorizationLifetime = TimeSpan.FromMinutes(10);

    /// <param name="allowedCorsOrigins">The web app origins (IdentitySettings:Web:AllowedOrigins); the token endpoint answers browsers from nowhere else.</param>
    /// <param name="bffOrigins">The BFF origins (IdentitySettings:WebBff:Origins).</param>
    /// <param name="bffPublicKeys">The BFF's public keys as JSON Web Keys (current first, then those kept during a rotation).</param>
    public static IEnumerable<Client> Clients(IEnumerable<string> allowedCorsOrigins, IReadOnlyCollection<string> bffOrigins, IEnumerable<string> bffPublicKeys) =>
    [
        new Client
        {
            AllowedCorsOrigins = [.. allowedCorsOrigins],
            ClientId = WebClientId,
            ClientName = "Harmony web",
            // Password first; when the account has two-factor on, the second call is the mfa_otp grant with the challenge and the code.
            AllowedGrantTypes = { GrantType.ResourceOwnerPassword, MfaOtpGrantValidator.GrantTypeName },
            RequireClientSecret = false,
            AllowedScopes = { PlatformScope, IdentityServerConstants.StandardScopes.OfflineAccess },
            AllowOfflineAccess = true,
            AccessTokenLifetime = (int)AccessTokenLifetime.TotalSeconds,
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = (int)RefreshIdleLifetime.TotalSeconds,
            AbsoluteRefreshTokenLifetime = (int)RefreshAbsoluteLifetime.TotalSeconds,
            UpdateAccessTokenClaimsOnRefresh = true,
        },

        // RFC 10017 §6.1: the BFF is a confidential client using the authorization code grant with PKCE; the tokens stay on it.
        new Client
        {
            ClientId = WebBffSettings.ClientId,
            ClientName = "Harmony web (BFF)",

            // private_key_jwt (RFC 7523): the BFF signs, Identity checks with the public key. No shared secret exists.
            ClientSecrets = [.. bffPublicKeys.Select(jwk => new Secret { Type = IdentityServerConstants.SecretTypes.JsonWebKey, Value = jwk })],
            RequireClientSecret = true,

            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            AllowPlainTextPkce = false,

            // PAR (RFC 9126): the sign-in details are pushed server to server; the browser carries only a one-time reference.
            RequirePushedAuthorization = true,
            PushedAuthorizationLifetime = (int)PushedAuthorizationLifetime.TotalSeconds,

            RedirectUris = [.. bffOrigins.Select(origin => origin + WebBffSettings.RedirectPath)],
            PostLogoutRedirectUris = [.. bffOrigins.Select(origin => origin + WebBffSettings.PostLogoutRedirectPath)],

            // A server-side client: no browser calls Identity's endpoints for it, so no CORS origin.
            AllowedCorsOrigins = [],

            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
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
        },
    ];
}
