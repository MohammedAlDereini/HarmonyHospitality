namespace Harmony.Identity.Handler.Commands.ConfigModule.Values;

using Harmony.Core.Abstractions;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

/// <summary>
/// The value at a place goes, so the next lower place wins again. A switched-off setting can still be cleared (tidying
/// is always allowed); nothing set there is 00113. The same place and write rules as Set.
/// </summary>
public class ClearConfigValueCommandHandler : ConfigValueCommandHandlerBase, IRequestHandler<ClearConfigValueCommand, CallResponse>
{
    public ClearConfigValueCommandHandler(
        IConfigSettingRepository settings,
        IConfigValueRepository values,
        IPropertyRepository properties,
        IPropertyGroupRepository groups,
        IPlatformPermissionResolver permissions,
        IHttpContextAccessor http,
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        ICacheService cache)
        : base(settings, values, properties, groups, permissions, http, tenants, userAccounts, auditEvents, securityEvents, callContext, platform, cache)
    {
    }

    public async Task<CallResponse> Handle(ClearConfigValueCommand command, CancellationToken cancellationToken)
    {
        var setting = await this.Settings.FindAsync(command.Key.Trim(), cancellationToken);
        if (setting is null)
        {
            return Fail(BusinessErrorCodes.Identity.Config.SettingNotFound);
        }

        var place = await this.PlaceAsync(ConfigValueRules.LevelOf(command.Scope)!.Value, command.ScopeId, null, cancellationToken);
        if (place.Refusal is not null)
        {
            return Fail(place.Refusal);
        }

        if (!await this.MayWriteAsync(place.Scope!, cancellationToken))
        {
            return Fail(BusinessErrorCodes.Identity.Config.CustomerWideOnly);
        }

        var row = await this.Values.FindAsync(this.CallerTenant, place.Scope!, setting.Key, cancellationToken);
        if (row is null)
        {
            return Fail(BusinessErrorCodes.Identity.Config.ValueNotFound);
        }

        row.Clear();
        this.Values.Remove(row);
        await this.TrailAsync(SecurityAuditActions.ConfigValueCleared, $"{setting.Key}@{place.Code}", cancellationToken);
        await this.Values.SaveChangesAsync(cancellationToken);
        await this.ForgetAsync(cancellationToken);
        return Ok();
    }
}
