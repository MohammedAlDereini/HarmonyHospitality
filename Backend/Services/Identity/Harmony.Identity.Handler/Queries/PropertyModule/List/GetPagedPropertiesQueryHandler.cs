using Harmony.Core.Abstractions;
using Harmony.Core.Identity.Subscriptions;
using Harmony.Identity.Domain.Models.PropertyModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.PropertyModule.List;

/// <summary>
/// The hotels, for the platform console (C4): one customer's or everyone's, narrowed by country, type, state and the
/// licence toggle; the page shown gets its customer's code, the rooms licensed today against the rooms declared, and
/// the modules with a line. A caller outside the platform sees nothing (00073).
/// </summary>
public class GetPagedPropertiesQueryHandler : IRequestHandler<GetPagedPropertiesQuery, CallResponse<PagedResult<PropertyModel>>>
{
    public const string StateActive = "Active";
    public const string StateOpening = "Opening";
    public const string StateClosed = "Closed";

    private readonly IPropertyRepository properties;
    private readonly ITenantRepository tenants;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;
    private readonly Guid platformTenantId;

    public GetPagedPropertiesQueryHandler(IPropertyRepository properties, ITenantRepository tenants, ICallContext callContext, TimeProvider clock, IOptions<PlatformTenantSettings> platform)
    {
        this.properties = properties;
        this.tenants = tenants;
        this.callContext = callContext;
        this.clock = clock;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<PagedResult<PropertyModel>>> Handle(GetPagedPropertiesQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return CallResponseBuilder.CreateResponse<PagedResult<PropertyModel>>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var keyword = request.SearchText?.Trim();
        var country = request.CountryCode?.Trim();
        var type = request.PropertyTypeCode?.Trim();
        var state = request.State?.Trim();
        var today = DateOnly.FromDateTime(this.clock.GetUtcNow().UtcDateTime);

        var query = this.properties.QueryAcrossTenants();
        query = query.WhereIf(request.TenantId.HasValue, e => e.TenantId == request.TenantId!.Value);
        query = query.WhereIf(request.IsActive.HasValue, e => e.IsActive == request.IsActive!.Value);
        query = query.WhereIf(country.IsNotNullOrEmpty(), e => e.Location.CountryCode == country);
        query = query.WhereIf(type.IsNotNullOrEmpty(), e => e.PropertyTypeCode == type);
        query = query.WhereIf(state == StateOpening, e => e.IsActive && e.OpensOn != null && e.OpensOn > today);
        query = query.WhereIf(state == StateClosed, e => !e.IsActive || (e.ClosesOn != null && e.ClosesOn < today));
        query = query.WhereIf(state == StateActive, e => e.IsActive && (e.OpensOn == null || e.OpensOn <= today) && (e.ClosesOn == null || e.ClosesOn >= today));
        query = query.WhereIf(
            keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
            e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!) || (e.ExternalIdentifier != null && e.ExternalIdentifier.Contains(keyword!)));

        // The licence, hotel by hotel: the largest line held today. Read once for the toggle, reused for the page.
        var lines = await this.tenants.Query()
            .SelectMany(t => t.Subscriptions)
            .Select(s => new { s.TenantId, s.PropertyId, s.ModuleCode, s.IsEnabled, s.RenewsAutomatically, s.Rooms, s.Terminals, s.StartsOn, s.EndsOn })
            .ToListAsync(cancellationToken);
        var licensedByHotel = lines
            .Where(s => s.PropertyId != null && s.Rooms != null)
            .Where(s => new SubscriptionLine(s.PropertyId, s.ModuleCode, s.IsEnabled, s.RenewsAutomatically, s.Rooms, s.Terminals, s.StartsOn, s.EndsOn).IsHeldOn(today))
            .GroupBy(s => s.PropertyId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(s => s.Rooms!.Value));

        if (request.AboveLicenceOnly)
        {
            var declared = await this.properties.QueryAcrossTenants().Select(p => new { p.Id, p.TotalRooms }).ToListAsync(cancellationToken);
            var above = declared.Where(p => licensedByHotel.TryGetValue(p.Id, out var max) && p.TotalRooms > max).Select(p => p.Id).ToList();
            query = query.Where(e => above.Contains(e.Id));
        }

        var pagedResult = await query.Select(PropertyProjections.Model).GetPagedResultAsync(request, true, cancellationToken);

        var tenantIds = pagedResult.Results.Select(r => r.TenantId).Distinct().ToList();
        var codes = tenantIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await this.tenants.Query().Where(t => tenantIds.Contains(t.Id)).Select(t => new { t.Id, t.Code }).ToDictionaryAsync(t => t.Id, t => t.Code, cancellationToken);

        foreach (var row in pagedResult.Results)
        {
            row.TenantCode = codes.TryGetValue(row.TenantId, out var code) ? code : null;
            row.LicensedRooms = licensedByHotel.TryGetValue(row.Id, out var licensed) ? licensed : null;
            row.RoomsAboveLicence = row.LicensedRooms is { } max && row.TotalRooms > max;
            row.ModuleCodes = lines
                .Where(l => l.IsEnabled && (l.PropertyId == row.Id || (l.PropertyId == null && l.TenantId == row.TenantId)))
                .Select(l => l.ModuleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return CallResponseBuilder.CreateResponse<PagedResult<PropertyModel>>(eCallResponseStatus.Success).HasData(pagedResult);
    }
}
