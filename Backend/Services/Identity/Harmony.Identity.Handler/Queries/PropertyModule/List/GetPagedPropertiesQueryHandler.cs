using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.PropertyModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.PropertyModule.List;

/// <summary>The hotels, for the platform console: one customer's or everyone's. A caller outside the platform sees nothing (00073).</summary>
public class GetPagedPropertiesQueryHandler : IRequestHandler<GetPagedPropertiesQuery, CallResponse<PagedResult<PropertyModel>>>
{
    private readonly IPropertyRepository properties;
    private readonly ICallContext callContext;
    private readonly Guid platformTenantId;

    public GetPagedPropertiesQueryHandler(IPropertyRepository properties, ICallContext callContext, IOptions<PlatformTenantSettings> platform)
    {
        this.properties = properties;
        this.callContext = callContext;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<PagedResult<PropertyModel>>> Handle(GetPagedPropertiesQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return CallResponseBuilder.CreateResponse<PagedResult<PropertyModel>>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var keyword = request.SearchText?.Trim();

        var query = this.properties.QueryAcrossTenants();
        query = query.WhereIf(request.TenantId.HasValue, e => e.TenantId == request.TenantId!.Value);
        query = query.WhereIf(request.IsActive.HasValue, e => e.IsActive == request.IsActive!.Value);
        query = query.WhereIf(
            keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
            e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!) || (e.ExternalIdentifier != null && e.ExternalIdentifier.Contains(keyword!)));

        var pagedResult = await query.Select(PropertyProjections.Model).GetPagedResultAsync(request, true, cancellationToken);
        return CallResponseBuilder.CreateResponse<PagedResult<PropertyModel>>(eCallResponseStatus.Success).HasData(pagedResult);
    }
}
