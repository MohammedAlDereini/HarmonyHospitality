using System.Globalization;
using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Harmony.Core.Abstractions;
using Harmony.Core.Identity.Implementations.Platform;
using Harmony.Identity.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Harmony.Identity.Infrastructure.IdentityServer;

/// <summary>
/// grant_type=harmony:service — tenant_id + principal + secret → an access token for that machine.
/// The endpoint is anonymous, so the tenant is a request parameter, never a header; the secret check
/// is what proves the caller may name it. Every refusal says the same thing: which part was wrong is
/// not something a caller gets to learn.
/// </summary>
public sealed class ServicePrincipalGrantValidator(
    IServiceProvider services,
    ICallContext callContext,
    IMultiTenancyService tenants) : IExtensionGrantValidator
{
    private const string Refused = "The service principal or secret is not valid.";

    public string GrantType => ServicePrincipalGrant.GrantType;

    public async Task ValidateAsync(ExtensionGrantValidationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var raw = context.Request.Raw;
        var code = raw.Get(ServicePrincipalGrant.PrincipalParameter);
        var secret = raw.Get(ServicePrincipalGrant.SecretParameter);

        if (!Guid.TryParse(raw.Get(ServicePrincipalGrant.TenantParameter), out var tenantId) || tenantId == Guid.Empty
            || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(secret)
            || !tenants.GetTenants().Any(t => string.Equals(t.Id, tenantId.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, Refused);
            return;
        }

        // This call runs under the tenant the request names: the framework opens that tenant's database.
        callContext.SetTenantId(tenantId.ToString());

        // Resolved now, not in the constructor: discovery lists the grant types and must not open a database.
        var userAccounts = services.GetRequiredService<IUserAccountRepository>();
        var user = await userAccounts.FindServicePrincipalAsync(tenantId, code, cancellationToken);

        // VerifyServiceCredential is false for a missing, suspended or terminated principal, and compares in constant time.
        if (user is null || !user.VerifyServiceCredential(secret))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, Refused);
            return;
        }

        var claims = new List<Claim>
        {
            new(PlatformClaimTypes.Tenant, user.TenantId.ToString()),
            new("name", user.DisplayName),
            new("security_version", user.SecurityVersion.ToString(CultureInfo.InvariantCulture)),
        };

        context.Result = new GrantValidationResult(user.Id.ToString(), ServicePrincipalGrant.AuthenticationMethod, claims);
    }
}
