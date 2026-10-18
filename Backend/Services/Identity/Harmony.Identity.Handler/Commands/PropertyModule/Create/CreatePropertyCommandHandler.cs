namespace Harmony.Identity.Handler.Commands.PropertyModule.Create;

using Harmony.Core.Abstractions;
using Harmony.Core.Exceptions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// The sale becomes rows. Every gate refuses before anything is written. Then one save: the hotel (inside the
/// customer's tenant, its TenantId set explicitly), the lines (platform data, on the customer), the trail. The
/// framework publishes PropertyCreatedDomainEvent after the commit; the cache publish hangs on it (change 8).
/// The hotel's rooms are saved whatever the licence says: the gap is flagged and trailed, never refused (Mews).
/// </summary>
public class CreatePropertyCommandHandler : PropertyCommandHandlerBase, IRequestHandler<CreatePropertyCommand, CallResponse<Guid>>
{
    /// <summary>The code the base context gives every unique-index violation; here it can only be the hotel code.</summary>
    private const string ConflictCode = "Conflict";

    private readonly ILogger<CreatePropertyCommandHandler> logger;

    public CreatePropertyCommandHandler(
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
        TimeProvider clock,
        ILogger<CreatePropertyCommandHandler> logger)
        : base(tenants, properties, userAccounts, auditEvents, securityEvents, callContext, platform, propertyTypes, cities, currencies, languages, environments, modules, clock)
    {
        this.logger = logger;
    }

    public async Task<CallResponse<Guid>> Handle(CreatePropertyCommand command, CancellationToken cancellationToken)
    {
        // 1. Platform only, never aimed at the platform's own tenant.
        if (!this.CallerIsPlatform || this.IsPlatformTenant(command.TenantId))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        // 2. The customer with its contract, tracked.
        var tenant = await this.Tenants.FindWithSubscriptionsAsync(command.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.NotActive);
        }

        // 3. The reference rows: they exist and are active.
        var type = await this.PropertyTypes.GetByCodeAsync(command.PropertyTypeCode.Trim(), cancellationToken);
        if (type is null || !type.IsActive)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Reference.NotFound);
        }

        var city = await this.Cities.GetByIdAsync(command.CityId, cancellationToken);
        if (city is null || !city.IsActive)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Reference.NotFound);
        }

        var currency = await this.Currencies.GetByCodeAsync(command.DefaultCurrency.Trim().ToUpperInvariant(), cancellationToken);
        if (currency is null || !currency.IsActive)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Reference.UnknownCurrency);
        }

        var language = await this.Languages.GetByCodeAsync(command.DefaultLanguageCode.Trim().ToLowerInvariant(), cancellationToken);
        if (language is null || !language.IsActive)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Reference.NotFound);
        }

        // 4. The value objects; the regimes must be real, active, of the right kind and of the city's country.
        var location = Location.Of(city);
        var address = new PostalAddress(command.AddressLine, command.PostalCode);
        var regulatory = new RegulatoryProfile(command.LegalEnvironmentCode, command.TaxEnvironmentCode, command.AccountingEnvironmentCode, command.AccommodationEnvironmentCode);
        if (!await this.RegimesBelongAsync(regulatory, location.CountryCode, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Property.EnvironmentMismatch);
        }

        // 5. One code per customer: the first answer; the unique index is the last.
        var code = command.Code.Trim().ToUpperInvariant();
        if (await this.Properties.CodeExistsInTenantAsync(tenant.Id, code, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Property.CodeInUse);
        }

        // 6. Every line names a real module that is sold per hotel.
        var modules = new List<Module>(command.Lines.Count);
        foreach (var line in command.Lines)
        {
            var module = await this.Modules.GetByCodeAsync(line.ModuleCode.Trim(), cancellationToken);
            if (module is null)
            {
                return Fail<Guid>(BusinessErrorCodes.Identity.Subscription.UnknownModule);
            }

            if (module.Level != ModuleLevel.Property)
            {
                return Fail<Guid>(BusinessErrorCodes.Identity.Subscription.ModuleLevelMismatch);
            }

            modules.Add(module);
        }

        // 7. The hotel first, so the context assigns its id; then its lines on the customer.
        var property = Property.Create(
            tenant.Id,
            code,
            command.NameEn,
            command.NameAr,
            type.Code,
            location,
            address,
            currency.Code,
            language.Code,
            command.TotalRooms,
            regulatory,
            command.OpensOn,
            command.ClosesOn,
            command.ExternalIdentifier);
        await this.Properties.AddAsync(property, cancellationToken);

        var lines = command.Lines
            .Zip(modules, (request, module) => tenant.Subscribe(property.Id, module.Code, request.Rooms, request.Terminals, command.StartsOn, command.EndsOn, request.RenewsAutomatically))
            .ToList();

        // 8. The trail: who sold what; and the rooms gap, flagged for finance, on the day the lines take effect.
        await this.TrailAsync(SecurityAuditActions.PropertyCreated, property.Code, cancellationToken);
        await this.TrailAsync(SecurityAuditActions.TenantSubscribed, $"{property.Code}:{string.Join(',', lines.Select(l => l.ModuleCode))}", cancellationToken);

        var effectiveDay = command.StartsOn > this.UtcToday ? command.StartsOn : this.UtcToday;
        var licensed = tenant.LicensedRooms(property.Id, effectiveDay);
        if (licensed is { } max && property.TotalRooms > max)
        {
            await this.TrailAsync(SecurityAuditActions.PropertyRoomsAboveLicence, $"{property.Code}:{property.TotalRooms}>{max}", cancellationToken);
            this.logger.LogInformation("Property {Code} of tenant {Tenant} has {Rooms} rooms against {Licensed} licensed; flagged, not refused.", property.Code, tenant.Code, property.TotalRooms, max);
        }

        // 9. One save = one transaction. Two console users racing on the same code: the index wins, and the base
        //    context's "Conflict" becomes the readable refusal.
        try
        {
            await this.Tenants.SaveChangesAsync(cancellationToken);
        }
        catch (BusinessException exception) when (exception.Errors.Any(error => error.Code == ConflictCode))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Property.CodeInUse);
        }

        this.logger.LogInformation("Property {Code} created for tenant {Tenant} with {Lines} subscription lines.", property.Code, tenant.Code, lines.Count);
        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(property.Id);
    }
}
