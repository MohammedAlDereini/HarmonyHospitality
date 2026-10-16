namespace Harmony.Identity.Handler.Commands.TenantModule.Create;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// The platform creates a customer: the row, Active from today, with a code nobody else has and a country and currency
/// from the reference tables. The customer's own administrator, catalogue and role are born in the next step; this door
/// writes nothing inside the new tenant.
/// </summary>
public class CreateTenantCommandHandler : TenantCommandHandlerBase, IRequestHandler<CreateTenantCommand, CallResponse<Guid>>
{
    private readonly ILogger<CreateTenantCommandHandler> logger;

    public CreateTenantCommandHandler(ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform, ILogger<CreateTenantCommandHandler> logger)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.logger = logger;
    }

    public async Task<CallResponse<Guid>> Handle(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        // The entity trims and upper-cases the codes, so every check below runs on the stored form.
        var tenant = Domain.Entities.Aggregates.TenantModule.Tenant.Create(command.Code, command.LegalNameEn, command.LegalNameAr, command.CountryCode, command.DefaultCurrency, command.Tier, command.ExternalIdentifier, DateOnly.FromDateTime(DateTime.UtcNow));

        if (!await this.Tenants.CountryExistsAsync(tenant.CountryCode!, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Reference.UnknownCountry);
        }

        if (!await this.Tenants.CurrencyExistsAsync(tenant.DefaultCurrency!, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Reference.UnknownCurrency);
        }

        if (await this.Tenants.CodeExistsAsync(tenant.Code, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.CodeInUse);
        }

        await this.Tenants.AddAsync(tenant, cancellationToken);
        await this.TrailAsync(SecurityAuditActions.TenantCreated, tenant, cancellationToken);
        await this.Tenants.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Tenant {Code} created as {TenantId} (tier {Tier}).", tenant.Code, tenant.Id, tenant.Tier);
        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(tenant.Id);
    }
}
