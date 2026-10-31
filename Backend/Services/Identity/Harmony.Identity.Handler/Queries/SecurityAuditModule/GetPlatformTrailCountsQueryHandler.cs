using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.SecurityAuditModule;

/// <summary>Counts per action over a window, across every customer; platform tenant only (00073 otherwise).</summary>
public class GetPlatformTrailCountsQueryHandler : IRequestHandler<GetPlatformTrailCountsQuery, CallResponse<List<TrailCountModel>>>
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(24);

    private readonly ISecurityAuditEventRepository events;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;
    private readonly Guid platformTenantId;

    public GetPlatformTrailCountsQueryHandler(ISecurityAuditEventRepository events, ICallContext callContext, TimeProvider clock, IOptions<PlatformTenantSettings> platform)
    {
        this.events = events;
        this.callContext = callContext;
        this.clock = clock;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<List<TrailCountModel>>> Handle(GetPlatformTrailCountsQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return CallResponseBuilder.CreateResponse<List<TrailCountModel>>(eCallResponseStatus.BusinessValidation)
                .HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var now = this.clock.GetUtcNow();
        var from = request.FromUtc ?? now.Subtract(DefaultWindow);
        var to = request.ToUtc ?? now;
        var action = request.Action?.Trim();

        var counts = await this.events.QueryAcrossTenants()
            .Where(e => e.OccurredAtUtc >= from && e.OccurredAtUtc < to)
            .WhereIf(request.TenantId.HasValue, e => e.TenantId == request.TenantId!.Value)
            .WhereIf(action.IsNotNullOrEmpty(), e => e.Action.StartsWith(action!))
            .GroupBy(e => e.Action)
            .Select(g => new TrailCountModel { Action = g.Key, Count = g.Count() })
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Action)
            .ToListAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<List<TrailCountModel>>(eCallResponseStatus.Success).HasData(counts);
    }
}
