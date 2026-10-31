using Harmony.Identity.Domain.Models.PropertyGroupModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.PropertyGroupModule.Mine;

/// <summary>One of the caller's groups, inside the wall; another customer's group is 00096, no hint.</summary>
public class GetMyPropertyGroupByIdQueryHandler : IRequestHandler<GetMyPropertyGroupByIdQuery, CallResponse<PropertyGroupModel>>
{
    private readonly IPropertyGroupRepository groups;
    private readonly IPropertyGroupMemberRepository members;
    private readonly IPropertyRepository properties;

    public GetMyPropertyGroupByIdQueryHandler(IPropertyGroupRepository groups, IPropertyGroupMemberRepository members, IPropertyRepository properties)
    {
        this.groups = groups;
        this.members = members;
        this.properties = properties;
    }

    public async Task<CallResponse<PropertyGroupModel>> Handle(GetMyPropertyGroupByIdQuery request, CancellationToken cancellationToken)
    {
        var model = await this.groups.Query()
            .Where(g => g.Id == request.Id)
            .Select(PropertyGroupProjections.Model)
            .FirstOrDefaultAsync(cancellationToken);
        if (model is null)
        {
            return CallResponseBuilder.CreateResponse<PropertyGroupModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.PropertyGroup.NotFound));
        }

        model.Members = await PropertyGroupProjections.MembersOf(this.members.Query(), this.properties.Query(), model.Id).ToListAsync(cancellationToken);
        model.CurrentMembers = model.Members.Count(m => m.IsCurrent);

        return CallResponseBuilder.CreateResponse<PropertyGroupModel>(eCallResponseStatus.Success).HasData(model);
    }
}
