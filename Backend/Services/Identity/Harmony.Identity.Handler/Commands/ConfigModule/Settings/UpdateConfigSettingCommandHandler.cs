namespace Harmony.Identity.Handler.Commands.ConfigModule.Settings;

using Harmony.Core.Abstractions;
using Harmony.Core.Exceptions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.TenantModule;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class UpdateConfigSettingCommandHandler : ConfigSettingCommandHandlerBase, IRequestHandler<UpdateConfigSettingCommand, CallResponse>
{
    public UpdateConfigSettingCommandHandler(
        IConfigSettingRepository settings,
        IPropertyTypeRepository propertyTypes,
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        ICacheService cache)
        : base(settings, propertyTypes, tenants, userAccounts, auditEvents, securityEvents, callContext, platform, cache)
    {
    }

    public async Task<CallResponse> Handle(UpdateConfigSettingCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var setting = await this.Settings.FindAsync(command.Key.Trim(), cancellationToken);
        if (setting is null)
        {
            return Fail(BusinessErrorCodes.Identity.Config.SettingNotFound);
        }

        if (!await this.TypesExistAsync(command.AppliesToTypes, cancellationToken))
        {
            return Fail(BusinessErrorCodes.Identity.Config.PropertyTypeNotFound);
        }

        setting.Update(command.OwnerService, command.SystemDefault, command.AppliesToTypes, command.IsActive);
        await this.TrailAsync(SecurityAuditActions.ConfigSettingUpdated, setting.Key, cancellationToken);
        await this.Settings.SaveChangesAsync(cancellationToken);
        await this.ForgetAllAsync(cancellationToken);
        return Ok();
    }
}
