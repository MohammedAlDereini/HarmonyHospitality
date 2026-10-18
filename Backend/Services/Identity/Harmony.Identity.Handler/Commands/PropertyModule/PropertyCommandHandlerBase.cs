namespace Harmony.Identity.Handler.Commands.PropertyModule;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.TenantModule;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>
/// What the property doors share with the tenant doors: platform-only, the trail on the acting platform person, and
/// the reference reads a hotel needs. Extends the tenant base so the gates are written once. The clock is .NET's
/// TimeProvider, so a test can set the day.
/// </summary>
public abstract class PropertyCommandHandlerBase : TenantCommandHandlerBase
{
    protected PropertyCommandHandlerBase(
        ITenantRepository tenants,
        IPropertyRepository properties,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        IPropertyTypeRepository propertyTypes,
        ICityRepository cities,
        ICurrencyRepository currencies,
        ILanguageRepository languages,
        IRegulatoryEnvironmentRepository environments,
        IModuleRepository modules,
        TimeProvider clock)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.Properties = properties;
        this.PropertyTypes = propertyTypes;
        this.Cities = cities;
        this.Currencies = currencies;
        this.Languages = languages;
        this.Environments = environments;
        this.Modules = modules;
        this.Clock = clock;
    }

    protected IPropertyRepository Properties { get; }

    protected IPropertyTypeRepository PropertyTypes { get; }

    protected ICityRepository Cities { get; }

    protected ICurrencyRepository Currencies { get; }

    protected ILanguageRepository Languages { get; }

    protected IRegulatoryEnvironmentRepository Environments { get; }

    protected IModuleRepository Modules { get; }

    protected TimeProvider Clock { get; }

    /// <summary>Today as a UTC calendar day: the day the contract dates are compared on.</summary>
    protected DateOnly UtcToday => DateOnly.FromDateTime(this.Clock.GetUtcNow().UtcDateTime);

    /// <summary>The four regimes exist, have the right kind, and belong to the hotel's country (the table carries no active flag).</summary>
    protected async Task<bool> RegimesBelongAsync(RegulatoryProfile regulatory, string countryCode, CancellationToken cancellationToken)
    {
        foreach (var (kind, code) in regulatory.Entries())
        {
            var row = await this.Environments.GetByCodeAsync(code, cancellationToken);
            if (row is null || row.Kind != kind || row.CountryCode != countryCode)
            {
                return false;
            }
        }

        return true;
    }
}
