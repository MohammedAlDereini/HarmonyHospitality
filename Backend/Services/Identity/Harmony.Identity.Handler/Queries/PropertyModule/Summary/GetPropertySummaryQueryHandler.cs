using Harmony.Core.Abstractions;
using Harmony.Core.Identity.Subscriptions;
using Harmony.Identity.Domain.Models.PropertyModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.PropertyModule.Summary;

/// <summary>
/// The hotels in numbers: how many, how many active, how many open soon, how many came this month, the rooms they
/// declare against the rooms they license today, and the hotels above their licence. Licensed rooms follow the one
/// definition every service uses (the framework's SubscriptionLine.IsHeldOn, the hotel's largest held line). Platform
/// tenant only (00073 otherwise).
/// </summary>
public class GetPropertySummaryQueryHandler : IRequestHandler<GetPropertySummaryQuery, CallResponse<PropertySummaryModel>>
{
    public const int OpeningWithinDays = 90;
    public const int AboveLicenceShown = 5;

    private readonly IPropertyRepository properties;
    private readonly ITenantRepository tenants;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;
    private readonly Guid platformTenantId;

    public GetPropertySummaryQueryHandler(
        IPropertyRepository properties,
        ITenantRepository tenants,
        ICallContext callContext,
        TimeProvider clock,
        IOptions<PlatformTenantSettings> platform)
    {
        this.properties = properties;
        this.tenants = tenants;
        this.callContext = callContext;
        this.clock = clock;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<PropertySummaryModel>> Handle(GetPropertySummaryQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return CallResponseBuilder.CreateResponse<PropertySummaryModel>(eCallResponseStatus.BusinessValidation)
                .HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var now = this.clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var openingHorizon = today.AddDays(OpeningWithinDays);

        var hotels = await this.properties.QueryAcrossTenants()
            .Where(p => p.TenantId != this.platformTenantId)
            .Select(p => new
            {
                p.Id,
                p.TenantId,
                p.Code,
                p.NameEn,
                p.NameAr,
                p.TotalRooms,
                p.IsActive,
                p.OpensOn,
                p.CreatedDate,
            })
            .ToListAsync(cancellationToken);

        // Only lines that can license rooms: on, for one hotel, with a room count. Held or not is decided here, for today.
        var lines = await this.tenants.Query()
            .Where(t => t.Id != this.platformTenantId)
            .SelectMany(t => t.Subscriptions)
            .Where(s => s.IsEnabled && s.PropertyId != null && s.Rooms != null)
            .Select(s => new { s.PropertyId, s.ModuleCode, s.IsEnabled, s.RenewsAutomatically, s.Rooms, s.Terminals, s.StartsOn, s.EndsOn })
            .ToListAsync(cancellationToken);

        var licensedByHotel = lines
            .Where(s => new SubscriptionLine(s.PropertyId, s.ModuleCode, s.IsEnabled, s.RenewsAutomatically, s.Rooms, s.Terminals, s.StartsOn, s.EndsOn).IsHeldOn(today))
            .GroupBy(s => s.PropertyId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(s => s.Rooms!.Value));

        var active = hotels.Where(h => h.IsActive).ToList();
        var above = active
            .Select(h => new { Hotel = h, Licensed = licensedByHotel.TryGetValue(h.Id, out var rooms) ? rooms : (int?)null })
            .Where(x => x.Licensed is { } max && x.Hotel.TotalRooms > max)
            .OrderByDescending(x => x.Hotel.TotalRooms - x.Licensed!.Value)
            .ThenBy(x => x.Hotel.Code)
            .ToList();

        var summary = new PropertySummaryModel
        {
            Today = today,
            Total = hotels.Count,
            Active = active.Count,
            OpeningSoon = hotels.Count(h => h.OpensOn is { } opens && opens > today && opens <= openingHorizon),
            CreatedThisMonth = hotels.Count(h => h.CreatedDate >= monthStart),
            TotalRooms = active.Sum(h => h.TotalRooms),
            LicensedRooms = active.Sum(h => licensedByHotel.TryGetValue(h.Id, out var rooms) ? rooms : 0),
            AboveLicence = above.Count,
            AboveLicenceProperties = above
                .Take(AboveLicenceShown)
                .Select(x => new PropertyLicenceModel
                {
                    Id = x.Hotel.Id,
                    TenantId = x.Hotel.TenantId,
                    Code = x.Hotel.Code,
                    NameEn = x.Hotel.NameEn,
                    NameAr = x.Hotel.NameAr,
                    TotalRooms = x.Hotel.TotalRooms,
                    LicensedRooms = x.Licensed!.Value,
                })
                .ToList(),
        };

        return CallResponseBuilder.CreateResponse<PropertySummaryModel>(eCallResponseStatus.Success).HasData(summary);
    }
}
