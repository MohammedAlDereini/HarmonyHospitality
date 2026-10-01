using Harmony.Core.Identity.Implementations.Platform;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// How a machine gets a token: one Duende client for all services, and our own grant that checks the
/// service principal's secret. One client, not one per service, so 46 services never count as 46 clients.
/// The shared values come from the framework, which is what every calling service sends, so they cannot drift.
/// </summary>
public static class ServicePrincipalGrant
{
    public const string GrantType = PlatformServiceGrant.GrantType;
    public const string ClientId = PlatformServiceGrant.ClientId;
    public const string Scope = PlatformServiceGrant.Scope;
    public const string AuthenticationMethod = "service";

    public const string TenantParameter = PlatformServiceGrant.TenantParameter;
    public const string PrincipalParameter = PlatformServiceGrant.PrincipalParameter;
    public const string SecretParameter = PlatformServiceGrant.SecretParameter;

    public const int AccessTokenLifetimeSeconds = 15 * 60;
}
