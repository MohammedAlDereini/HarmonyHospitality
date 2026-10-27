namespace Harmony.Identity.Handler.Commands.CompanyLoginModule.Add;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.TenantModule;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AddCompanyLoginCommandHandler : TenantCommandHandlerBase, IRequestHandler<AddCompanyLoginCommand, CallResponse<Guid>>
{
    private readonly IFederatedAuthorityRepository authorities;
    private readonly ICompanyLoginDirectory directory;
    private readonly ILogger<AddCompanyLoginCommandHandler> logger;

    public AddCompanyLoginCommandHandler(
        IFederatedAuthorityRepository authorities,
        ICompanyLoginDirectory directory,
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        ILogger<AddCompanyLoginCommandHandler> logger)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.authorities = authorities;
        this.directory = directory;
        this.logger = logger;
    }

    public async Task<CallResponse<Guid>> Handle(AddCompanyLoginCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var tenant = await this.Tenants.FindAsync(command.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        var scheme = CompanyLoginRules.ProviderOf(command.Provider)!.Value;
        if (!this.directory.IsConfigured(scheme))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.CompanyLogin.ProviderNotConfigured);
        }

        if ((await this.authorities.OfTenantAsync(tenant.Id, cancellationToken)).Any(login => login.Scheme == scheme))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.CompanyLogin.AlreadyExists);
        }

        var domain = FederatedAuthority.DomainName(command.Domain);
        if (FederatedAuthority.IsPublicMailDomain(domain))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.CompanyLogin.PublicDomain);
        }

        if (await this.authorities.DomainListedAsync(domain, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.CompanyLogin.DomainInUse);
        }

        var issuer = await this.directory.IssuerOfAsync(scheme, domain, cancellationToken);
        if (issuer is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.CompanyLogin.CompanyNotFound);
        }

        // One Microsoft company signs in to one customer: its people would otherwise land in two.
        if (scheme == AuthorityScheme.Microsoft && await this.authorities.IssuerInUseAsync(issuer, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.CompanyLogin.CompanyInUse);
        }

        var login = FederatedAuthority.Company(tenant.Id, scheme, issuer, this.directory.AudienceOf(scheme));
        login.AddDomain(domain);

        await this.authorities.AddAsync(login, cancellationToken);
        await this.TrailAsync(SecurityAuditActions.CompanyLoginAdded, $"{scheme}@{tenant.Code} {domain}", cancellationToken);
        await this.authorities.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Company login {Scheme} added to tenant {Code} ({Issuer}).", scheme, tenant.Code, issuer);
        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(login.Id);
    }
}
