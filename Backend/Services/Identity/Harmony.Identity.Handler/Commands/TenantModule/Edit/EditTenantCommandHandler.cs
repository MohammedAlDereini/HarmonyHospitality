namespace Harmony.Identity.Handler.Commands.TenantModule.Edit;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>The names, country, currency, tier and billing reference change; the code and the status never do here.</summary>
public class EditTenantCommandHandler : TenantCommandHandlerBase, IRequestHandler<EditTenantCommand, CallResponse>
{
    public EditTenantCommandHandler(ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
    }

    public async Task<CallResponse> Handle(EditTenantCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var tenant = await this.Tenants.FindAsync(command.Id, cancellationToken);
        if (tenant is null)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        // A terminated tenant is history: nothing in it changes.
        if (tenant.Status == Domain.Entities.Aggregates.TenantModule.TenantStatus.Terminated)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.NotActive);
        }

        var countryCode = command.CountryCode.Trim().ToUpperInvariant();
        var currency = command.DefaultCurrency.Trim().ToUpperInvariant();
        if (!await this.Tenants.CountryExistsAsync(countryCode, cancellationToken))
        {
            return Fail(BusinessErrorCodes.Identity.Reference.UnknownCountry);
        }

        if (!await this.Tenants.CurrencyExistsAsync(currency, cancellationToken))
        {
            return Fail(BusinessErrorCodes.Identity.Reference.UnknownCurrency);
        }

        tenant.Update(command.LegalNameEn, command.LegalNameAr, countryCode, currency, command.Tier, command.ExternalIdentifier);
        await this.TrailAsync(SecurityAuditActions.TenantUpdated, tenant, cancellationToken);
        await this.Tenants.SaveChangesAsync(cancellationToken);

        return Ok();
    }
}
