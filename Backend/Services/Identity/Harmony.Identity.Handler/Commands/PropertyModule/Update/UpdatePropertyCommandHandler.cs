namespace Harmony.Identity.Handler.Commands.PropertyModule.Update;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.TenantModule;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>
/// The hotel's own door and the console's: inside the wall the filter finds the hotel (another customer's is not
/// found, no hint); the platform reaches a customer's hotel from outside the wall. The aggregate takes the change,
/// the trail is written on the acting person, one save. A room count that moved above the licence is flagged in the
/// trail, never refused (Mews).
/// </summary>
public class UpdatePropertyCommandHandler : TenantCommandHandlerBase, IRequestHandler<UpdatePropertyCommand, CallResponse>
{
    private readonly IPropertyRepository properties;
    private readonly TimeProvider clock;

    public UpdatePropertyCommandHandler(
        ITenantRepository tenants,
        IPropertyRepository properties,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        TimeProvider clock)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.properties = properties;
        this.clock = clock;
    }

    public async Task<CallResponse> Handle(UpdatePropertyCommand command, CancellationToken cancellationToken)
    {
        var property = this.CallerIsPlatform
            ? await this.properties.FindAcrossTenantsAsync(command.Id, cancellationToken)
            : await this.properties.FindAsync(command.Id, cancellationToken);
        if (property is null)
        {
            return Fail(BusinessErrorCodes.Identity.Property.NotFound);
        }

        var roomsBefore = property.TotalRooms;
        property.Update(command.NameEn, command.NameAr, new PostalAddress(command.AddressLine, command.PostalCode), command.OpensOn, command.ClosesOn, command.TotalRooms);
        property.UpdateDetails(
            command.ShortName, command.UniqueName, command.Latitude, command.Longitude,
            command.BusinessId, command.BusinessRegistrationCode, command.BusinessPremiseId,
            command.FiscalYearBeginMonth, command.FiscalYearBeginDay, command.DefaultCulture, command.StarRating,
            command.Email, command.Telephone, command.WebsiteUrl, command.ProductChampionEmail, command.LegalOwnerName, command.DisplayOrder);
        await this.TrailAsync(SecurityAuditActions.PropertyUpdated, property.Code, cancellationToken);

        if (property.TotalRooms != roomsBefore)
        {
            var tenant = await this.Tenants.FindWithSubscriptionsAsync(property.TenantId, cancellationToken);
            var today = DateOnly.FromDateTime(this.clock.GetUtcNow().UtcDateTime);
            if (tenant?.LicensedRooms(property.Id, today) is { } licensed && property.TotalRooms > licensed)
            {
                await this.TrailAsync(SecurityAuditActions.PropertyRoomsAboveLicence, $"{property.Code}:{property.TotalRooms}>{licensed}", cancellationToken);
            }
        }

        await this.Tenants.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
