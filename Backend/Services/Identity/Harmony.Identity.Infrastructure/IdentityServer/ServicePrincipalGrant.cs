namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// How a machine gets a token: one Duende client for all services, and our own grant that checks the
/// service principal's secret. One client, not one per service, so 46 services never count as 46 clients.
/// </summary>
public static class ServicePrincipalGrant
{
    public const string GrantType = "harmony:service";
    public const string ClientId = "harmony.services";
    public const string Scope = "harmony";
    public const string AuthenticationMethod = "service";

    public const string TenantParameter = "tenant_id";
    public const string PrincipalParameter = "principal";
    public const string SecretParameter = "secret";

    public const int AccessTokenLifetimeSeconds = 15 * 60;
}
