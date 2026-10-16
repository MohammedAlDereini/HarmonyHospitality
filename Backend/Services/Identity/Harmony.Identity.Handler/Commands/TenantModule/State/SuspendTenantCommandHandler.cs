namespace Harmony.Identity.Handler.Commands.TenantModule.State;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// The wall closes: from the save on, no one in the tenant signs in, with a password or a passkey, and no session in it
/// refreshes (Identity's IsActive check), so every session ends within the access token's life. The platform's own
/// tenant is never suspended: that door does not exist (00073).
/// </summary>
public class SuspendTenantCommandHandler : TenantCommandHandlerBase, IRequestHandler<SuspendTenantCommand, CallResponse>
{
    private readonly ILogger<SuspendTenantCommandHandler> logger;

    public SuspendTenantCommandHandler(ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform, ILogger<SuspendTenantCommandHandler> logger)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(SuspendTenantCommand command, CancellationToken cancellationToken)
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

        if (tenant.Status == TenantStatus.Terminated)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.NotActive);
        }

        // Suspending twice is a no-op, and so is its trail.
        var before = tenant.Status;
        tenant.Suspend(command.Reason);
        if (tenant.Status != before)
        {
            await this.TrailAsync(SecurityAuditActions.TenantSuspended, tenant, cancellationToken);
            this.logger.LogWarning("Tenant {Code} suspended: nobody in it signs in until it is reinstated.", tenant.Code);
        }

        await this.Tenants.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
