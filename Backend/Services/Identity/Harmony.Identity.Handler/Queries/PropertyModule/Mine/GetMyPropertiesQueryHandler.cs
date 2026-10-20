using Harmony.Identity.Domain.Models.PropertyModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.PropertyModule.Mine;

/// <summary>The caller's hotels: the repository's walled query, nothing else. The platform tenant has none and gets an empty page.</summary>
public class GetMyPropertiesQueryHandler : IRequestHandler<GetMyPropertiesQuery, CallResponse<PagedResult<PropertyModel>>>
{
    private readonly IPropertyRepository properties;

    public GetMyPropertiesQueryHandler(IPropertyRepository properties)
    {
        this.properties = properties;
    }

    public async Task<CallResponse<PagedResult<PropertyModel>>> Handle(GetMyPropertiesQuery request, CancellationToken cancellationToken)
    {
        var keyword = request.SearchText?.Trim();

        var query = this.properties.Query();
        query = query.WhereIf(request.IsActive.HasValue, e => e.IsActive == request.IsActive!.Value);
        query = query.WhereIf(
            keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
            e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!) || (e.ExternalIdentifier != null && e.ExternalIdentifier.Contains(keyword!)));

        var pagedResult = await query.Select(PropertyProjections.Model).GetPagedResultAsync(request, true, cancellationToken);
        return CallResponseBuilder.CreateResponse<PagedResult<PropertyModel>>(eCallResponseStatus.Success).HasData(pagedResult);
    }
}
