namespace Harmony.Identity.Handler.Commands.TenantModule.Subscriptions.Subscribe;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// A line born (201) or renewed (200). Gates first; then the line on the tracked customer, the trail, one save; the customer's
/// event rewrites the cache after the commit. A hotel's rooms above the renewed licence are flagged, never refused.
/// </summary>
public class SubscribeCommandHandler : SubscriptionCommandHandlerBase, IRequestHandler<SubscribeCommand, CallResponse<Guid>>
{
    private readonly ILogger<SubscribeCommandHandler> logger;

    public SubscribeCommandHandler(
        ITenantRepository tenants,
        IPropertyRepository properties,
        IModuleRepository modules,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        TimeProvider clock,
        ILogger<SubscribeCommandHandler> logger)
        : base(tenants, properties, modules, userAccounts, auditEvents, securityEvents, callContext, platform, clock)
    {
        this.logger = logger;
    }

    public async Task<CallResponse<Guid>> Handle(SubscribeCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform || this.IsPlatformTenant(command.TenantId))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var tenant = await this.Tenants.FindWithSubscriptionsAsync(command.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.NotActive);
        }

        var (module, property, error) = await this.TargetAsync(tenant.Id, command.PropertyId, command.ModuleCode, cancellationToken);
        if (error is not null)
        {
            return Fail<Guid>(error);
        }

        var line = tenant.Subscribe(property?.Id, module!.Code, command.Rooms, command.Terminals, command.StartsOn, command.EndsOn, command.RenewsAutomatically);
        var born = line.Id == Guid.Empty; // a new row gets its id when the save tracks it; a renewed one already has it
        await this.TrailAsync(SecurityAuditActions.TenantSubscribed, LineName(property, module.Code), cancellationToken);

        if (property is not null)
        {
            var effectiveDay = command.StartsOn > this.UtcToday ? command.StartsOn : this.UtcToday;
            var licensed = tenant.LicensedRooms(property.Id, effectiveDay);
            if (licensed is { } max && property.TotalRooms > max)
            {
                await this.TrailAsync(SecurityAuditActions.PropertyRoomsAboveLicence, $"{property.Code}:{property.TotalRooms}>{max}", cancellationToken);
            }
        }

        await this.Tenants.SaveChangesAsync(cancellationToken);
        this.logger.LogInformation("Tenant {Tenant}: line {Line} subscribed.", tenant.Code, LineName(property, module.Code));
        return CallResponseBuilder.CreateResponse<Guid>(born ? eCallResponseStatus.Created : eCallResponseStatus.Success).HasData(line.Id);
    }
}
