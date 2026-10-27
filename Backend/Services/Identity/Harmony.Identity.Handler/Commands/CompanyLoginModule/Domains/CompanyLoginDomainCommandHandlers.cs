namespace Harmony.Identity.Handler.Commands.CompanyLoginModule.Domains;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>
/// A domain joins the list: never a public mail domain (00125), never one already listed anywhere (00123). While the login
/// is on, the new domain must still leave an administrator on a Harmony password (00127).
/// </summary>
public class AddCompanyLoginDomainCommandHandler : CompanyLoginCommandHandlerBase, IRequestHandler<AddCompanyLoginDomainCommand, CallResponse>
{
    public AddCompanyLoginDomainCommandHandler(
        IFederatedAuthorityRepository authorities,
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform)
        : base(authorities, tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
    }

    public async Task<CallResponse> Handle(AddCompanyLoginDomainCommand command, CancellationToken cancellationToken)
    {
        var login = await this.CompanyLoginAsync(command.Id, cancellationToken);
        if (login is null)
        {
            return Fail(BusinessErrorCodes.Identity.CompanyLogin.NotFound);
        }

        var domain = FederatedAuthority.DomainName(command.Domain);
        if (FederatedAuthority.IsPublicMailDomain(domain))
        {
            return Fail(BusinessErrorCodes.Identity.CompanyLogin.PublicDomain);
        }

        if (await this.Authorities.DomainListedAsync(domain, cancellationToken))
        {
            return Fail(BusinessErrorCodes.Identity.CompanyLogin.DomainInUse);
        }

        login.AddDomain(domain);
        if (!await this.FallbackHoldsAsync(login, cancellationToken))
        {
            return Fail(BusinessErrorCodes.Identity.CompanyLogin.FallbackAdministratorRequired);
        }

        await this.TrailAsync(SecurityAuditActions.CompanyLoginDomainAdded, domain, cancellationToken);
        await this.Authorities.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}

/// <summary>A domain leaves the list (00124 when it is not on it). The last one cannot go while the login is on (00126).</summary>
public class RemoveCompanyLoginDomainCommandHandler : CompanyLoginCommandHandlerBase, IRequestHandler<RemoveCompanyLoginDomainCommand, CallResponse>
{
    public RemoveCompanyLoginDomainCommandHandler(
        IFederatedAuthorityRepository authorities,
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform)
        : base(authorities, tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
    }

    public async Task<CallResponse> Handle(RemoveCompanyLoginDomainCommand command, CancellationToken cancellationToken)
    {
        var login = await this.CompanyLoginAsync(command.Id, cancellationToken);
        if (login is null)
        {
            return Fail(BusinessErrorCodes.Identity.CompanyLogin.NotFound);
        }

        var domain = FederatedAuthority.DomainName(command.Domain);
        if (login.Domains.All(d => d.Domain != domain))
        {
            return Fail(BusinessErrorCodes.Identity.CompanyLogin.DomainNotFound);
        }

        if (login.IsEnabled && login.Domains.Count == 1)
        {
            return Fail(BusinessErrorCodes.Identity.CompanyLogin.NoDomains);
        }

        login.RemoveDomain(domain);
        await this.TrailAsync(SecurityAuditActions.CompanyLoginDomainRemoved, domain, cancellationToken);
        await this.Authorities.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
