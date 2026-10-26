using Harmony.Identity.Domain.Models.PropertyGroupModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.PropertyGroupModule.Mine;

/// <summary>The caller's groups: the repository's walled query, nothing else. The platform tenant has none and gets an empty page.</summary>
public class GetMyPropertyGroupsQueryHandler : IRequestHandler<GetMyPropertyGroupsQuery, CallResponse<PagedResult<PropertyGroupModel>>>
{
    private readonly IPropertyGroupRepository groups;
    private readonly IPropertyGroupMemberRepository members;

    public GetMyPropertyGroupsQueryHandler(IPropertyGroupRepository groups, IPropertyGroupMemberRepository members)
    {
        this.groups = groups;
        this.members = members;
    }

    public async Task<CallResponse<PagedResult<PropertyGroupModel>>> Handle(GetMyPropertyGroupsQuery request, CancellationToken cancellationToken)
    {
        var query = PropertyGroupProjections.Filtered(this.groups.Query(), request.TypeCode, request.SearchText, request.IsActive);
        var pagedResult = await query.Select(PropertyGroupProjections.Model).GetPagedResultAsync(request, true, cancellationToken);
        await PropertyGroupProjections.FillCurrentMembersAsync(pagedResult.Results, this.members.Query(), cancellationToken);
        return CallResponseBuilder.CreateResponse<PagedResult<PropertyGroupModel>>(eCallResponseStatus.Success).HasData(pagedResult);
    }
}
