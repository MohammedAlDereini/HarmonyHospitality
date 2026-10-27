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

/// <summary>
/// The registry doors share the tenant doors' gates: platform only (00073 otherwise), the trail on the acting platform
/// person. Every narrowing names real property types (00117) before anything is written.
/// </summary>
public abstract class ConfigSettingCommandHandlerBase : TenantCommandHandlerBase
{
    /// <summary>The code the base context gives every unique-index violation; on these doors it can only be the key.</summary>
    protected const string ConflictCode = "Conflict";

    protected ConfigSettingCommandHandlerBase(
        IConfigSettingRepository settings,
        IPropertyTypeRepository propertyTypes,
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
        this.PropertyTypes = propertyTypes;
    }

    protected IConfigSettingRepository Settings { get; }

    protected IPropertyTypeRepository PropertyTypes { get; }

    protected ICacheService Cache { get; }

    /// <summary>After the save: every hotel copy of every customer goes, the key list itself changed.</summary>
    protected Task ForgetAllAsync(CancellationToken cancellationToken)
        => this.Cache.DeleteByPrefixAsync(Harmony.Core.Identity.Settings.SettingsCacheKeys.AllHotelSettings, cancellationToken);

    /// <summary>Every code of the narrowing is a row of PropertyTypes.</summary>
    protected async Task<bool> TypesExistAsync(IEnumerable<string>? codes, CancellationToken cancellationToken)
    {
        foreach (var code in (codes ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!await this.PropertyTypes.ExistsAsync(code, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }
}
