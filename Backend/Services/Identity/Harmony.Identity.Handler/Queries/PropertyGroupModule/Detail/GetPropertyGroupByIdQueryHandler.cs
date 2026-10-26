using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.PropertyGroupModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.PropertyGroupModule.Detail;

/// <summary>One group, for the platform console. A caller outside the platform sees nothing (00073); an unknown id is 00096.</summary>
public class GetPropertyGroupByIdQueryHandler : IRequestHandler<GetPropertyGroupByIdQuery, CallResponse<PropertyGroupModel>>
{
    private readonly IPropertyGroupRepository groups;
    private readonly IPropertyGroupMemberRepository members;
    private readonly IPropertyRepository properties;
    private readonly ICallContext callContext;
    private readonly Guid platformTenantId;

    public GetPropertyGroupByIdQueryHandler(IPropertyGroupRepository groups, IPropertyGroupMemberRepository members, IPropertyRepository properties, ICallContext callContext, IOptions<PlatformTenantSettings> platform)
    {
        this.groups = groups;
        this.members = members;
        this.properties = properties;
        this.callContext = callContext;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<PropertyGroupModel>> Handle(GetPropertyGroupByIdQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return Refuse(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var model = await this.groups.QueryAcrossTenants()
            .Where(g => g.Id == request.Id)
            .Select(PropertyGroupProjections.Model)
            .FirstOrDefaultAsync(cancellationToken);
        if (model is null)
        {
            return Refuse(BusinessErrorCodes.Identity.PropertyGroup.NotFound);
        }

        model.Brand = await this.groups.QueryAcrossTenants().Where(g => g.Id == model.Id).Select(PropertyGroupProjections.Brand).FirstOrDefaultAsync(cancellationToken);
        model.Members = await PropertyGroupProjections.MembersOf(this.members.QueryAcrossTenants(), this.properties.QueryAcrossTenants(), model.Id).ToListAsync(cancellationToken);
        model.CurrentMembers = model.Members.Count(m => m.IsCurrent);

        return CallResponseBuilder.CreateResponse<PropertyGroupModel>(eCallResponseStatus.Success).HasData(model);
    }

    private static CallResponse<PropertyGroupModel> Refuse(string code)
        => CallResponseBuilder.CreateResponse<PropertyGroupModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(code));
}
