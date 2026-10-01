using Duende.IdentityServer.Models;
using Harmony.Core.Identity.Implementations.Platform;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// What Duende may issue tokens for. In code, not in a database: there is one platform audience and
/// a handful of clients, and a client is a deployment decision that ships with the code that uses it.
/// </summary>
public static class IdentityServerResources
{
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
        new ApiScope(ServicePrincipalGrant.Scope, "Harmony platform"),
    ];

    /// <summary>The platform audience: every service validates "aud: harmony" (IdentitySettings:Platform:ValidAudiences).</summary>
    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource(ServicePrincipalGrant.Scope, "Harmony platform")
        {
            Scopes = { ServicePrincipalGrant.Scope },
            UserClaims = UserClaims,
        },
    ];

    public static IEnumerable<Client> Clients =>
    [
        // The credential is the service principal's secret, checked by the grant; the client itself is public.
        new Client
        {
            ClientId = ServicePrincipalGrant.ClientId,
            ClientName = "Harmony services",
            AllowedGrantTypes = { ServicePrincipalGrant.GrantType },
            RequireClientSecret = false,
            AllowedScopes = { ServicePrincipalGrant.Scope },
            AccessTokenLifetime = ServicePrincipalGrant.AccessTokenLifetimeSeconds,
            AllowOfflineAccess = false,
        },
    ];
}
