namespace Harmony.Identity.Handler.Commands.CompanyLoginModule;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.TenantModule;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>
/// The customer administrator's company login doors: the caller's own customer is the wall, and only its company logins
/// answer (never the local one, never another customer's). The trail lands on the caller.
/// </summary>
public abstract class CompanyLoginCommandHandlerBase : TenantCommandHandlerBase
{
    protected CompanyLoginCommandHandlerBase(
        IFederatedAuthorityRepository authorities,
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.Authorities = authorities;
    }

    protected IFederatedAuthorityRepository Authorities { get; }

    /// <summary>The caller's own customer: the token's tenant.</summary>
    protected Guid CallerTenant => Guid.Parse(this.CallContext.TenantId());

    /// <summary>One of the caller's customer's company logins, tracked; null for anything else.</summary>
    protected async Task<FederatedAuthority?> CompanyLoginAsync(Guid id, CancellationToken cancellationToken)
    {
        var login = await this.Authorities.FindAsync(this.CallerTenant, id, cancellationToken);
        return login is { IsCompany: true } ? login : null;
    }

    /// <summary>The fallback administrator rule, with this company login as changed in memory.</summary>
    protected async Task<bool> FallbackHoldsAsync(FederatedAuthority changed, CancellationToken cancellationToken)
        => await CompanyLoginRules.FallbackHoldsAsync(
            this.UserAccounts,
            changed.TenantId,
            await CompanyLoginRules.WithChangedAsync(this.Authorities, changed, cancellationToken),
            personId: null,
            personPasswordSignIn: null,
            cancellationToken);
}
