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
        "security_version",
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

    public static IEnumerable<Client> Clients => [];
}
