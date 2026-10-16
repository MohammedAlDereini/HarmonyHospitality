namespace Harmony.Identity.Handler.Commands.TenantModule.State;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class ReinstateTenantCommandHandler : TenantCommandHandlerBase, IRequestHandler<ReinstateTenantCommand, CallResponse>
{
    private readonly ILogger<ReinstateTenantCommandHandler> logger;

    public ReinstateTenantCommandHandler(ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform, ILogger<ReinstateTenantCommandHandler> logger)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(ReinstateTenantCommand command, CancellationToken cancellationToken)
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

        if (tenant.Status == TenantStatus.Terminated)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.NotActive);
        }

        // Reinstating an active tenant is a no-op, and so is its trail.
        var before = tenant.Status;
        tenant.Reinstate();
        if (tenant.Status != before)
        {
            await this.TrailAsync(SecurityAuditActions.TenantReinstated, tenant, cancellationToken);
            this.logger.LogInformation("Tenant {Code} reinstated.", tenant.Code);
        }

        await this.Tenants.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
