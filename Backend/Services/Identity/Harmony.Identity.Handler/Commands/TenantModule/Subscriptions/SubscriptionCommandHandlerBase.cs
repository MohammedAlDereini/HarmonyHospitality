namespace Harmony.Identity.Handler.Commands.TenantModule.Subscriptions;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>
/// What the contract doors share: the tenant gates, and the one question both ask before touching a line: is the
/// module real, is it sold on the floor the line names, and is the hotel this customer's.
/// </summary>
public abstract class SubscriptionCommandHandlerBase : TenantCommandHandlerBase
{
    protected SubscriptionCommandHandlerBase(
        ITenantRepository tenants,
        IPropertyRepository properties,
        IModuleRepository modules,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        TimeProvider clock)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.Properties = properties;
        this.Modules = modules;
        this.Clock = clock;
    }

    protected IPropertyRepository Properties { get; }

    protected IModuleRepository Modules { get; }

    protected TimeProvider Clock { get; }

    /// <summary>Today as a UTC calendar day: the day the contract dates are compared on.</summary>
    protected DateOnly UtcToday => DateOnly.FromDateTime(this.Clock.GetUtcNow().UtcDateTime);

    /// <summary>The line's target: the module (its stored spelling) and the hotel, or the refusal code.</summary>
    protected async Task<(Module? Module, Property? Property, string? Error)> TargetAsync(Guid tenantId, Guid? propertyId, string moduleCode, CancellationToken cancellationToken)
    {
        var module = await this.Modules.GetByCodeAsync(moduleCode.Trim(), cancellationToken);
        if (module is null)
        {
            return (null, null, BusinessErrorCodes.Identity.Subscription.UnknownModule);
        }

        if (propertyId is { } id)
        {
            if (module.Level != ModuleLevel.Property)
            {
                return (null, null, BusinessErrorCodes.Identity.Subscription.ModuleLevelMismatch);
            }

            var property = await this.Properties.FindInTenantAsync(tenantId, id, cancellationToken);
            return property is null
                ? (null, null, BusinessErrorCodes.Identity.Property.NotFound)
                : (module, property, null);
        }

        return module.Level != ModuleLevel.Tenant
            ? (null, null, BusinessErrorCodes.Identity.Subscription.ModuleLevelMismatch)
            : (module, null, null);
    }

    /// <summary>"AMMAN:epos" for a hotel line, "-:loyalty" for a chain-wide one.</summary>
    protected static string LineName(Property? property, string moduleCode) => $"{property?.Code ?? "-"}:{moduleCode}";
}
