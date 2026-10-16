namespace Harmony.Identity.Handler.Commands.TenantModule.Create;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// A new customer, born with its administrator (platform only). The row and its trail are written first, in the
/// platform's own tenant; then, inside the new tenant for exactly as long as it takes, the tenant-scope catalogue, the
/// ADMINISTRATOR system role and the first account, invited by e-mail with a one-time 24-hour link. The code is chosen
/// once and stored upper-case; the country and the currency must be reference rows; one e-mail is one person on the
/// whole platform, so an address held elsewhere is refused before anything is written.
/// </summary>
public class CreateTenantCommandHandler : TenantAdministratorHandlerBase, IRequestHandler<CreateTenantCommand, CallResponse<Guid>>
{
    private readonly ILogger<CreateTenantCommandHandler> logger;

    public CreateTenantCommandHandler(
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        IPermissionRepository permissions,
        IRoleRepository roles,
        UserManager<User> userManager,
        IAccountMailer mailer,
        ICacheService cacheService,
        ILogger<CreateTenantCommandHandler> logger)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform, permissions, roles, userManager, mailer, cacheService, logger)
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

        var email = command.AdministratorEmail.Trim();
        var displayName = command.AdministratorDisplayName.Trim();

        // One e-mail is one person: refused before anything is written, so nothing is left behind.
        if (await this.AdministratorEmailTakenElsewhereAsync(tenant.Id, email, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.AdministratorEmailTaken);
        }

        // The row and its trail first, in the platform's own tenant; then the birth inside the new one.
        await this.Tenants.AddAsync(tenant, cancellationToken);
        await this.TrailAsync(SecurityAuditActions.TenantCreated, tenant, cancellationToken);
        await this.Tenants.SaveChangesAsync(cancellationToken);
        this.logger.LogInformation("Tenant {Code} created as {TenantId} (tier {Tier}).", tenant.Code, tenant.Id, tenant.Tier);

        // Born with its administrator. A mail that cannot be sent does not undo the tenant (decided 2026-10-16): the trail
        // says tenant.administrator-invitation-failed and InviteAdministrator sends it again.
        var birth = await this.ProvisionAdministratorAsync(tenant, email, displayName, cancellationToken);
        if (birth.Errors.Count > 0)
        {
            return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.BusinessValidation).HasErrors(birth.Errors.ToArray());
        }

        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(tenant.Id);
    }
}
