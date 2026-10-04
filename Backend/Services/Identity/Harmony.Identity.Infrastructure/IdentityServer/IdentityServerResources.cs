using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Harmony.Core.Identity.Implementations.Platform;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// What Duende may issue tokens for. In code, not in a database: one platform audience, a handful of clients.
/// Clients arrive with the sign-in flows (S5). 🔴 SERVICES: the machine client lived here; decided at the services step.
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

    /// <summary>The web client people sign in with: PropX email + password, issued by Duende as the password grant.</summary>
    public const string WebClientId = "harmony-web";

    /// <summary>15 minutes: a token is short-lived; a demoted or terminated user is stopped sooner by security_version anyway.</summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>The design docs session rule: 30 minutes idle, 12 hours absolute, enforced by the refresh token.</summary>
    public static readonly TimeSpan RefreshIdleLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan RefreshAbsoluteLifetime = TimeSpan.FromHours(12);

    public static IEnumerable<Client> Clients =>
    [
        new Client
        {
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
    ];
}
