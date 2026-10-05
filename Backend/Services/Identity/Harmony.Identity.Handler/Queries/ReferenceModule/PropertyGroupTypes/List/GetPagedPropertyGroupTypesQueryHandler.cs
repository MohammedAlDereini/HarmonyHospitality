using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.PropertyGroupTypes.List
{
    public class GetPagedPropertyGroupTypesQueryHandler : IRequestHandler<GetPagedPropertyGroupTypesQuery, CallResponse<PagedResult<PropertyGroupTypeModel>>>
    {
        private readonly IPropertyGroupTypeRepository repository;

        public GetPagedPropertyGroupTypesQueryHandler(IPropertyGroupTypeRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<PropertyGroupTypeModel>>> Handle(GetPagedPropertyGroupTypesQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.PropertyGroupType).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<PropertyGroupTypeModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
