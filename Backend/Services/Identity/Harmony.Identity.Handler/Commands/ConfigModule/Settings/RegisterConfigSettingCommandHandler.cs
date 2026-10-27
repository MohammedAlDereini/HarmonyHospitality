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

public class RegisterConfigSettingCommandHandler : ConfigSettingCommandHandlerBase, IRequestHandler<RegisterConfigSettingCommand, CallResponse<string>>
{
    private readonly ILogger<RegisterConfigSettingCommandHandler> logger;

    public RegisterConfigSettingCommandHandler(
        IConfigSettingRepository settings,
        IPropertyTypeRepository propertyTypes,
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        ICacheService cache,
        ILogger<RegisterConfigSettingCommandHandler> logger)
        : base(settings, propertyTypes, tenants, userAccounts, auditEvents, securityEvents, callContext, platform, cache)
    {
        this.logger = logger;
    }

    public async Task<CallResponse<string>> Handle(RegisterConfigSettingCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform)
        {
            return Fail<string>(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var key = command.Key.Trim();
        if (await this.Settings.KeyExistsAsync(key, cancellationToken))
        {
            return Fail<string>(BusinessErrorCodes.Identity.Config.SettingKeyInUse);
        }

        if (!await this.TypesExistAsync(command.AppliesToTypes, cancellationToken))
        {
            return Fail<string>(BusinessErrorCodes.Identity.Config.PropertyTypeNotFound);
        }

        var setting = ConfigSetting.Register(key, command.OwnerService, command.DataType, command.SystemDefault, command.AppliesToTypes);
        await this.Settings.AddAsync(setting, cancellationToken);
        await this.TrailAsync(SecurityAuditActions.ConfigSettingRegistered, setting.Key, cancellationToken);

        try
        {
            await this.Settings.SaveChangesAsync(cancellationToken);
        }
        catch (BusinessException exception) when (exception.Errors.Any(error => error.Code == ConflictCode))
        {
            return Fail<string>(BusinessErrorCodes.Identity.Config.SettingKeyInUse);
        }

        await this.ForgetAllAsync(cancellationToken);
        this.logger.LogInformation("Setting {Key} ({Type}) registered for {Owner}.", setting.Key, setting.DataType, setting.OwnerService);
        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(setting.Key);
    }
}
