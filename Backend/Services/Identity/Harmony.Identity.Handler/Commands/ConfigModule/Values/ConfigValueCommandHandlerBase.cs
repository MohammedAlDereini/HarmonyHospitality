namespace Harmony.Identity.Handler.Commands.ConfigModule.Values;

using Harmony.Core.Abstractions;
using Harmony.Core.Identity.Implementations.Platform;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.TenantModule;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

/// <summary>
/// What the value doors share: the caller's own customer is the wall; the place must be one of its hotels or groups
/// (00114); and who may write where. ManageSettings from a hotel role reaches that hotel only, and only when the call
/// names it (X-Harmony-Property): a value for the whole customer or a group needs ManageSettings from a customer-wide
/// role (00116), or a hotel manager could change every hotel of the chain. The trail lands on the caller.
/// </summary>
public abstract class ConfigValueCommandHandlerBase : TenantCommandHandlerBase
{
    private readonly IHttpContextAccessor http;
    private readonly IPlatformPermissionResolver permissions;

    protected ConfigValueCommandHandlerBase(
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
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.Cache = cache;
        this.Settings = settings;
        this.Values = values;
        this.Properties = properties;
        this.Groups = groups;
        this.permissions = permissions;
        this.http = http;
    }

    protected IConfigSettingRepository Settings { get; }

    protected ICacheService Cache { get; }

    /// <summary>After the save: the customer's hotel copies go, so the next read resolves again.</summary>
    protected Task ForgetAsync(CancellationToken cancellationToken)
        => this.Cache.DeleteByPrefixAsync(Harmony.Core.Identity.Settings.SettingsCacheKeys.HotelSettingsPrefixOf(this.CallerTenant), cancellationToken);

    protected IConfigValueRepository Values { get; }

    protected IPropertyRepository Properties { get; }

    protected IPropertyGroupRepository Groups { get; }

    /// <summary>The caller's own customer: the token's tenant.</summary>
    protected Guid CallerTenant => Guid.Parse(this.CallContext.TenantId());

    /// <summary>
    /// The place named by the door, inside the caller's customer, with the words the trail uses; or the refusal.
    /// A hotel's place also answers whether the setting is for that hotel's type.
    /// </summary>
    protected async Task<Place> PlaceAsync(ConfigScopeLevel level, Guid? scopeId, ConfigSetting? setting, CancellationToken cancellationToken)
    {
        var tenantId = this.CallerTenant;
        switch (level)
        {
            case ConfigScopeLevel.Tenant:
            {
                var tenant = await this.Tenants.FindAsync(tenantId, cancellationToken);
                return tenant is null
                    ? Place.Refused(BusinessErrorCodes.Identity.Tenant.NotFound)
                    : new Place(ConfigScope.Tenant(tenantId), tenant.Code, null);
            }

            case ConfigScopeLevel.PropertyGroup:
            {
                var code = await this.Groups.Query().Where(g => g.Id == scopeId).Select(g => g.Code).FirstOrDefaultAsync(cancellationToken);
                return code is null
                    ? Place.Refused(BusinessErrorCodes.Identity.Config.ScopeNotFound)
                    : new Place(ConfigScope.Group(scopeId!.Value), code, null);
            }

            default:
            {
                var hotel = await this.Properties.Query().Where(p => p.Id == scopeId).Select(p => new { p.Code, p.PropertyTypeCode }).FirstOrDefaultAsync(cancellationToken);
                if (hotel is null)
                {
                    return Place.Refused(BusinessErrorCodes.Identity.Config.ScopeNotFound);
                }

                if (setting is not null && !setting.AppliesTo(hotel.PropertyTypeCode))
                {
                    return Place.Refused(BusinessErrorCodes.Identity.Config.NotForThisPropertyType);
                }

                return new Place(ConfigScope.Property(scopeId!.Value), hotel.Code, null);
            }
        }
    }

    /// <summary>
    /// May the caller write at this place. A hotel named on the call: the permission check already counted that
    /// hotel's roles, so the hotel itself is fine. Anywhere else: ManageSettings must come from the token's own roles.
    /// </summary>
    protected async Task<bool> MayWriteAsync(ConfigScope place, CancellationToken cancellationToken)
    {
        if (place.Level == ConfigScopeLevel.Property
            && Guid.TryParse(this.CallContext.PropertyId(), out var named)
            && named == place.Id)
        {
            return true;
        }

        var user = this.http.HttpContext?.User;
        var roleIds = user?.FindAll(PlatformClaimTypes.Role)
            .Select(claim => Guid.TryParse(claim.Value, out var roleId) ? roleId : Guid.Empty)
            .Where(roleId => roleId != Guid.Empty)
            .Distinct()
            ?? [];

        foreach (var roleId in roleIds)
        {
            if ((await this.permissions.GetPermissionsAsync(this.CallerTenant, roleId, cancellationToken)).Contains((int)PermissionEnum.ManageSettings))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A place inside the caller's customer, with its words for the trail; or the code that refused it.</summary>
    protected sealed record Place(ConfigScope? Scope, string? Code, string? Refusal)
    {
        public static Place Refused(string code) => new(null, null, code);
    }
}
