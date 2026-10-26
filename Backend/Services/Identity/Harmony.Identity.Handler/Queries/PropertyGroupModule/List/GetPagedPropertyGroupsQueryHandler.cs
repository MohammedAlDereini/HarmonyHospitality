using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.PropertyGroupModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.PropertyGroupModule.List;

/// <summary>The groups, for the platform console: one customer's or everyone's. A caller outside the platform sees nothing (00073).</summary>
public class GetPagedPropertyGroupsQueryHandler : IRequestHandler<GetPagedPropertyGroupsQuery, CallResponse<PagedResult<PropertyGroupModel>>>
{
    private readonly IPropertyGroupRepository groups;
    private readonly IPropertyGroupMemberRepository members;
    private readonly ICallContext callContext;
    private readonly Guid platformTenantId;

    public GetPagedPropertyGroupsQueryHandler(IPropertyGroupRepository groups, IPropertyGroupMemberRepository members, ICallContext callContext, IOptions<PlatformTenantSettings> platform)
    {
        this.groups = groups;
        this.members = members;
        this.callContext = callContext;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<PagedResult<PropertyGroupModel>>> Handle(GetPagedPropertyGroupsQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return CallResponseBuilder.CreateResponse<PagedResult<PropertyGroupModel>>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var query = this.groups.QueryAcrossTenants();
        query = query.WhereIf(request.TenantId.HasValue, e => e.TenantId == request.TenantId!.Value);
        query = PropertyGroupProjections.Filtered(query, request.TypeCode, request.SearchText, request.IsActive);

        var pagedResult = await query.Select(PropertyGroupProjections.Model).GetPagedResultAsync(request, true, cancellationToken);
        await PropertyGroupProjections.FillCurrentMembersAsync(pagedResult.Results, this.members.QueryAcrossTenants(), cancellationToken);
        return CallResponseBuilder.CreateResponse<PagedResult<PropertyGroupModel>>(eCallResponseStatus.Success).HasData(pagedResult);
    }
}
