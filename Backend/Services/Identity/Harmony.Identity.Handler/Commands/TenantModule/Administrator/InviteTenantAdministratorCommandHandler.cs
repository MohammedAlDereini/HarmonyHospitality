namespace Harmony.Identity.Handler.Commands.TenantModule.Administrator;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class InviteTenantAdministratorCommandHandler : TenantAdministratorHandlerBase, IRequestHandler<InviteTenantAdministratorCommand, CallResponse>
{
    public InviteTenantAdministratorCommandHandler(
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
        IFederatedAuthorityRepository authorities,
        ILocalStaffEnrolmentRepository enrolments,
        ILogger<InviteTenantAdministratorCommandHandler> logger)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform, permissions, roles, userManager, mailer, cacheService, authorities, enrolments, logger)
    {
    }

    public async Task<CallResponse> Handle(InviteTenantAdministratorCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform || this.IsPlatformTenant(command.Id))
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var tenant = await this.Tenants.FindAsync(command.Id, cancellationToken);
        if (tenant is null)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.NotActive);
        }

        // A re-send keeps the administrator's own address and name; a repair of a tenant born without an account needs both.
        var existing = await this.ExistingAdministratorAsync(tenant.Id, cancellationToken);
        var email = existing?.Email ?? command.AdministratorEmail?.Trim();
        var displayName = existing?.DisplayName ?? command.AdministratorDisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.AdministratorUnknown);
        }

        var outcome = await this.ProvisionAdministratorAsync(tenant, email, displayName, cancellationToken);
        return outcome.Errors.Count > 0
            ? CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(outcome.Errors.ToArray())
            : Ok();
    }
}
