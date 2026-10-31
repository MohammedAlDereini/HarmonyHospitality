using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.SecurityAuditModule;

/// <summary>
/// The trail across every customer, for the platform console. The tenant filter is lifted on purpose: this is the one
/// door that reads the whole trail, and it opens only for the platform tenant (00073 otherwise); every row still names
/// its tenant. The customer's and the person's names are read beside the rows; an actor that is an account of ours gets
/// its name after the page is cut.
/// </summary>
public class GetPagedPlatformTrailQueryHandler : IRequestHandler<GetPagedPlatformTrailQuery, CallResponse<PagedResult<PlatformTrailEventModel>>>
{
    private const int MinimumSearchLength = 2;

    private readonly ISecurityAuditEventRepository events;
    private readonly ITenantRepository tenants;
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;
    private readonly Guid platformTenantId;

    public GetPagedPlatformTrailQueryHandler(
        ISecurityAuditEventRepository events,
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform)
    {
        this.events = events;
        this.tenants = tenants;
        this.userAccounts = userAccounts;
        this.callContext = callContext;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<PagedResult<PlatformTrailEventModel>>> Handle(GetPagedPlatformTrailQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return CallResponseBuilder.CreateResponse<PagedResult<PlatformTrailEventModel>>(eCallResponseStatus.BusinessValidation)
                .HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var action = request.Action?.Trim();
        var actions = (request.Actions ?? []).Select(a => a.Trim()).Where(a => a.Length > 0).Distinct().ToArray();
        var keyword = request.SearchText?.Trim();
        var actor = request.ActorSubject?.Trim();
        var searching = keyword.IsNotNullOrEmpty() && keyword!.Length >= MinimumSearchLength;

        var trail = this.events.QueryAcrossTenants()
            .WhereIf(request.TenantId.HasValue, e => e.TenantId == request.TenantId!.Value)
            .WhereIf(request.UserAccountId.HasValue, e => e.UserAccountId == request.UserAccountId!.Value)
            .WhereIf(actor.IsNotNullOrEmpty(), e => e.ActorSubject == actor)
            .WhereIf(request.FromUtc.HasValue, e => e.OccurredAtUtc >= request.FromUtc!.Value)
            .WhereIf(request.ToUtc.HasValue, e => e.OccurredAtUtc < request.ToUtc!.Value)
            .WhereIf(action.IsNotNullOrEmpty(), e => e.Action.StartsWith(action!))
            .WhereIf(actions.Length > 0, e => actions.Contains(e.Action));

        var rows =
            from e in trail
            join t in this.tenants.Query() on e.TenantId equals t.Id into tenantRows
            from t in tenantRows.DefaultIfEmpty()
            join u in this.userAccounts.QueryAcrossTenants() on e.UserAccountId equals u.Id into userRows
            from u in userRows.DefaultIfEmpty()
            select new PlatformTrailEventModel
            {
                Id = e.Id,
                TenantId = e.TenantId,
                TenantCode = t != null ? t.Code : null,
                TenantNameEn = t != null ? t.LegalNameEn : null,
                TenantNameAr = t != null ? t.LegalNameAr : null,
                UserAccountId = e.UserAccountId,
                UserDisplayName = u != null ? u.DisplayName : null,
                Action = e.Action,
                ActorSubject = e.ActorSubject,
                SourceAddress = e.SourceAddress,
                Device = e.Device,
                OccurredAtUtc = e.OccurredAtUtc,
                Detail = e.Detail,
            };

        rows = rows.WhereIf(
            searching,
            r => r.Action.Contains(keyword!)
                || (r.Detail != null && r.Detail.Contains(keyword!))
                || r.ActorSubject.Contains(keyword!)
                || (r.SourceAddress != null && r.SourceAddress.Contains(keyword!))
                || (r.Device != null && r.Device.Contains(keyword!))
                || (r.TenantCode != null && r.TenantCode.Contains(keyword!))
                || (r.TenantNameEn != null && r.TenantNameEn.Contains(keyword!))
                || (r.TenantNameAr != null && r.TenantNameAr.Contains(keyword!))
                || (r.UserDisplayName != null && r.UserDisplayName.Contains(keyword!)));

        // The framework's sorter knows no DateTimeOffset, so the order is written here: the caller's one member, then
        // newest first, then the id, so a page never shifts under the person. The request is handed over without a sort.
        var ordered = Order(rows, request.SortOrder?.FirstOrDefault());
        request.SortOrder = [];
        var page = await ordered.GetPagedResultAsync(request, false, cancellationToken);
        await this.NameActorsAsync(page.Results, cancellationToken);

        return CallResponseBuilder.CreateResponse<PagedResult<PlatformTrailEventModel>>(eCallResponseStatus.Success).HasData(page);
    }

    private static IOrderedQueryable<PlatformTrailEventModel> Order(IQueryable<PlatformTrailEventModel> rows, SortOrderOption? wish)
    {
        var descending = wish?.SortOrder == eSortOrder.DEC;
        var member = wish?.MemberName ?? nameof(PlatformTrailEventModel.OccurredAtUtc);
        IOrderedQueryable<PlatformTrailEventModel> ordered;
        if (string.Equals(member, nameof(PlatformTrailEventModel.Action), StringComparison.OrdinalIgnoreCase))
        {
            ordered = descending ? rows.OrderByDescending(r => r.Action) : rows.OrderBy(r => r.Action);
        }
        else if (string.Equals(member, nameof(PlatformTrailEventModel.TenantCode), StringComparison.OrdinalIgnoreCase))
        {
            ordered = descending ? rows.OrderByDescending(r => r.TenantCode) : rows.OrderBy(r => r.TenantCode);
        }
        else if (string.Equals(member, nameof(PlatformTrailEventModel.UserDisplayName), StringComparison.OrdinalIgnoreCase))
        {
            ordered = descending ? rows.OrderByDescending(r => r.UserDisplayName) : rows.OrderBy(r => r.UserDisplayName);
        }
        else
        {
            ordered = descending ? rows.OrderByDescending(r => r.OccurredAtUtc) : rows.OrderBy(r => r.OccurredAtUtc);
        }

        return ordered.ThenByDescending(r => r.OccurredAtUtc).ThenBy(r => r.Id);
    }

    /// <summary>An actor written as an account id gets that account's name; "system" and ids no longer known stay as they are.</summary>
    private async Task NameActorsAsync(IList<PlatformTrailEventModel> rows, CancellationToken cancellationToken)
    {
        var ids = rows
            .Select(r => Guid.TryParse(r.ActorSubject, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var names = await this.userAccounts.QueryAcrossTenants()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        foreach (var row in rows)
        {
            if (Guid.TryParse(row.ActorSubject, out var id) && names.TryGetValue(id, out var name))
            {
                row.ActorDisplayName = name;
            }
        }
    }
}
