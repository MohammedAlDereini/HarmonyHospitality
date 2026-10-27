namespace Harmony.Identity.Handler.Commands.CompanyLoginModule.Switch;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>
/// The customer's administrator switches the company login on (the listed domains sign in with the company from the next
/// sign-in) or off (everybody uses a Harmony password again). Already so: nothing changes.
/// </summary>
public class SwitchCompanyLoginCommand : BaseCommandRequest<CallResponse>
{
    /// <summary>The company login.</summary>
    public Guid Id { get; set; }

    public bool On { get; set; }

    public class SwitchCompanyLoginCommandValidation : AbstractValidator<SwitchCompanyLoginCommand>
    {
        public SwitchCompanyLoginCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.CompanyLogin.IdRequired);
        }
    }
}

/// <summary>On needs a listed domain (00126) and an administrator left on a Harmony password (00127).</summary>
public class SwitchCompanyLoginCommandHandler : CompanyLoginCommandHandlerBase, IRequestHandler<SwitchCompanyLoginCommand, CallResponse>
{
    public SwitchCompanyLoginCommandHandler(
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

    public async Task<CallResponse> Handle(SwitchCompanyLoginCommand command, CancellationToken cancellationToken)
    {
        var login = await this.CompanyLoginAsync(command.Id, cancellationToken);
        if (login is null)
        {
            return Fail(BusinessErrorCodes.Identity.CompanyLogin.NotFound);
        }

        if (login.IsEnabled == command.On)
        {
            return Ok();
        }

        if (command.On)
        {
            if (login.Domains.Count == 0)
            {
                return Fail(BusinessErrorCodes.Identity.CompanyLogin.NoDomains);
            }

            login.SwitchOn();
            if (!await this.FallbackHoldsAsync(login, cancellationToken))
            {
                return Fail(BusinessErrorCodes.Identity.CompanyLogin.FallbackAdministratorRequired);
            }
        }
        else
        {
            login.SwitchOff();
        }

        await this.TrailAsync(command.On ? SecurityAuditActions.CompanyLoginSwitchedOn : SecurityAuditActions.CompanyLoginSwitchedOff, login.Scheme.ToString(), cancellationToken);
        await this.Authorities.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
