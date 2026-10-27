namespace Harmony.Identity.Handler.Commands.ConfigModule.Values;

using Harmony.Core.Abstractions;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

/// <summary>
/// Every gate refuses before anything is written: the key is registered (00110) and switched on (00112); the place is
/// the caller's (00114) and, for a hotel, of a type the setting is for (00115); the caller may write there (00116).
/// Then one save: the value (new or changed) and the trail.
/// </summary>
public class SetConfigValueCommandHandler : ConfigValueCommandHandlerBase, IRequestHandler<SetConfigValueCommand, CallResponse<Guid>>
{
    public SetConfigValueCommandHandler(
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

    public async Task<CallResponse<Guid>> Handle(SetConfigValueCommand command, CancellationToken cancellationToken)
    {
        var setting = await this.Settings.FindAsync(command.Key.Trim(), cancellationToken);
        if (setting is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Config.SettingNotFound);
        }

        if (!setting.IsActive)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Config.SettingInactive);
        }

        var place = await this.PlaceAsync(ConfigValueRules.LevelOf(command.Scope)!.Value, command.ScopeId, setting, cancellationToken);
        if (place.Refusal is not null)
        {
            return Fail<Guid>(place.Refusal);
        }

        if (!await this.MayWriteAsync(place.Scope!, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Config.CustomerWideOnly);
        }

        var existing = await this.Values.FindAsync(this.CallerTenant, place.Scope!, setting.Key, cancellationToken);
        var row = existing;
        if (row is null)
        {
            row = ConfigValue.Set(this.CallerTenant, place.Scope!, setting.Key, command.Value);
            await this.Values.AddAsync(row, cancellationToken);
        }
        else
        {
            row.Change(command.Value);
        }

        await this.TrailAsync(SecurityAuditActions.ConfigValueSet, $"{setting.Key}@{place.Code}", cancellationToken);
        await this.Values.SaveChangesAsync(cancellationToken);
        await this.ForgetAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<Guid>(existing is null ? eCallResponseStatus.Created : eCallResponseStatus.Success).HasData(row.Id);
    }
}
