using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.PropertyTypes.List
{
    public class GetPagedPropertyTypesQueryHandler : IRequestHandler<GetPagedPropertyTypesQuery, CallResponse<PagedResult<PropertyTypeModel>>>
    {
        private readonly IPropertyTypeRepository repository;

        public GetPagedPropertyTypesQueryHandler(IPropertyTypeRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<PropertyTypeModel>>> Handle(GetPagedPropertyTypesQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(request.IsActive.HasValue, e => e.IsActive == request.IsActive!.Value);
            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.PropertyType).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<PropertyTypeModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
