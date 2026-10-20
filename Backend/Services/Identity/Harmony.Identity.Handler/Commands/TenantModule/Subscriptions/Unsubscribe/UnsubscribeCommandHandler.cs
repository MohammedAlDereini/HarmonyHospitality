namespace Harmony.Identity.Handler.Commands.TenantModule.Subscriptions.Unsubscribe;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// A line switched off with a reason: not held from today whatever its term says, the row kept for the history.
/// The module table is not asked: a module retired from the catalogue can still be switched off. 00094 when the
/// customer never had the line.
/// </summary>
public class UnsubscribeCommandHandler : SubscriptionCommandHandlerBase, IRequestHandler<UnsubscribeCommand, CallResponse>
{
    private readonly ILogger<UnsubscribeCommandHandler> logger;

    public UnsubscribeCommandHandler(
        ITenantRepository tenants,
        IPropertyRepository properties,
        IModuleRepository modules,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        TimeProvider clock,
        ILogger<UnsubscribeCommandHandler> logger)
        : base(tenants, properties, modules, userAccounts, auditEvents, securityEvents, callContext, platform, clock)
    {
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(UnsubscribeCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform || this.IsPlatformTenant(command.TenantId))
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var tenant = await this.Tenants.FindWithSubscriptionsAsync(command.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.NotActive);
        }

        var line = tenant.Unsubscribe(command.PropertyId, command.ModuleCode, command.Reason);
        if (line is null)
        {
            return Fail(BusinessErrorCodes.Identity.Subscription.LineNotFound);
        }

        var property = command.PropertyId is { } id ? await this.Properties.FindInTenantAsync(tenant.Id, id, cancellationToken) : null;
        await this.TrailAsync(SecurityAuditActions.TenantUnsubscribed, $"{LineName(property, line.ModuleCode)}:{command.Reason.Trim()}", cancellationToken);

        await this.Tenants.SaveChangesAsync(cancellationToken);
        this.logger.LogInformation("Tenant {Tenant}: line {Line} switched off.", tenant.Code, LineName(property, line.ModuleCode));
        return Ok();
    }
}
