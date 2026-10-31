using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Models.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.TenantModule.Summary;

/// <summary>
/// The customers in numbers: how many in each state, in how many countries, how many came each of the last twelve
/// months, who is suspended, which fixed-term lines end soon. The platform's own row is left out. Platform tenant only
/// (00073 otherwise).
/// </summary>
public class GetTenantSummaryQueryHandler : IRequestHandler<GetTenantSummaryQuery, CallResponse<TenantSummaryModel>>
{
    public const int Months = 12;
    public const int SuspendedShown = 5;
    public const int EndingShown = 10;
    public const int EndingWithinDays = 30;

    private readonly ITenantRepository tenants;
    private readonly IPropertyRepository properties;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;
    private readonly Guid platformTenantId;

    public GetTenantSummaryQueryHandler(
        ITenantRepository tenants,
        IPropertyRepository properties,
        ICallContext callContext,
        TimeProvider clock,
        IOptions<PlatformTenantSettings> platform)
    {
        this.tenants = tenants;
        this.properties = properties;
        this.callContext = callContext;
        this.clock = clock;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<TenantSummaryModel>> Handle(GetTenantSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return CallResponseBuilder.CreateResponse<TenantSummaryModel>(eCallResponseStatus.BusinessValidation)
                .HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var today = DateOnly.FromDateTime(this.clock.GetUtcNow().UtcDateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var firstMonth = monthStart.AddMonths(1 - Months);
        var horizon = today.AddDays(EndingWithinDays);
        var customers = this.tenants.Query().Where(t => t.Id != this.platformTenantId);

        var byStatus = await customers
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countries = await customers
            .Where(t => t.Status == TenantStatus.Active && t.CountryCode != null)
            .Select(t => t.CountryCode)
            .Distinct()
            .CountAsync(cancellationToken);

        var onboarded = await customers
            .Where(t => t.OnboardedOn >= firstMonth)
            .Select(t => t.OnboardedOn)
            .ToListAsync(cancellationToken);

        var suspended = await customers
            .Where(t => t.Status == TenantStatus.Suspended)
            .OrderByDescending(t => t.ModifiedDate ?? t.CreatedDate)
            .Take(SuspendedShown)
            .Select(t => new TenantBriefModel
            {
                Id = t.Id,
                Code = t.Code,
                LegalNameEn = t.LegalNameEn,
                LegalNameAr = t.LegalNameAr,
                StatusReason = t.StatusReason,
                ModifiedDate = t.ModifiedDate,
            })
            .ToListAsync(cancellationToken);

        var ending = await customers
            .Where(t => t.Status == TenantStatus.Active)
            .SelectMany(t => t.Subscriptions
                .Where(s => s.IsEnabled && !s.RenewsAutomatically && s.EndsOn != null && s.EndsOn >= today && s.EndsOn <= horizon)
                .Select(s => new EndingLineModel
                {
                    TenantId = t.Id,
                    TenantCode = t.Code,
                    TenantNameEn = t.LegalNameEn,
                    TenantNameAr = t.LegalNameAr,
                    PropertyId = s.PropertyId,
                    ModuleCode = s.ModuleCode,
                    EndsOn = s.EndsOn!.Value,
                    Rooms = s.Rooms,
                }))
            .OrderBy(l => l.EndsOn)
            .ThenBy(l => l.TenantCode)
            .Take(EndingShown)
            .ToListAsync(cancellationToken);
        await this.NamePropertiesAsync(ending, cancellationToken);

        var summary = new TenantSummaryModel
        {
            Today = today,
            Active = byStatus.FirstOrDefault(s => s.Status == TenantStatus.Active)?.Count ?? 0,
            Suspended = byStatus.FirstOrDefault(s => s.Status == TenantStatus.Suspended)?.Count ?? 0,
            Terminated = byStatus.FirstOrDefault(s => s.Status == TenantStatus.Terminated)?.Count ?? 0,
            Countries = countries,
            OnboardedThisMonth = onboarded.Count(d => d >= monthStart),
            OnboardedByMonth = Enumerable.Range(0, Months)
                .Select(i => firstMonth.AddMonths(i))
                .Select(month => new MonthCountModel
                {
                    Month = $"{month.Year:D4}-{month.Month:D2}",
                    Count = onboarded.Count(d => d.Year == month.Year && d.Month == month.Month),
                })
                .ToList(),
            SuspendedTenants = suspended,
            LinesEndingSoon = ending,
        };

        return CallResponseBuilder.CreateResponse<TenantSummaryModel>(eCallResponseStatus.Success).HasData(summary);
    }

    private async Task NamePropertiesAsync(List<EndingLineModel> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Where(l => l.PropertyId.HasValue).Select(l => l.PropertyId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var codes = await this.properties.QueryAcrossTenants()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Code })
            .ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken);

        foreach (var line in lines)
        {
            if (line.PropertyId is { } id && codes.TryGetValue(id, out var code))
            {
                line.PropertyCode = code;
            }
        }
    }
}
