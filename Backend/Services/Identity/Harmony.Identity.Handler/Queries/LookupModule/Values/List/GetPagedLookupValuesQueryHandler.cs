using Harmony.Identity.Domain.Models.LookupModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.LookupModule.Values.List
{
    public class GetPagedLookupValuesQueryHandler : IRequestHandler<GetPagedLookupValuesQuery, CallResponse<PagedResult<LookupValueListModel>>>
    {
        private readonly ILookupValueRepository lookupValueRepository;

        public GetPagedLookupValuesQueryHandler(ILookupValueRepository lookupValueRepository)
        {
            this.lookupValueRepository = lookupValueRepository;
        }

        public async Task<CallResponse<PagedResult<LookupValueListModel>>> Handle(GetPagedLookupValuesQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim()?.ToLower();

            var query = this.lookupValueRepository.QueryForCategory(request.LookupCategoryId);

            query = query.WhereIf(request.SearchText.IsNotNullOrEmpty() && request.SearchText!.Length >= 2, v => v.Name.Contains(keyword!))
                .WhereIf(request.IsActive.HasValue, v => v.IsActive == request.IsActive!.Value);

            var projectedQuery = query.Select(v => new LookupValueListModel
            {
                Id = v.Id,
                LookupCategoryId = v.LookupCategoryId,
                Name = v.Name,
                Description = v.Description,
                IsActive = v.IsActive,
                IsGlobal = v.IsGlobal,
                CreatedDate = v.CreatedDate,
                CreatedBy = v.CreatedBy,
                ModifiedDate = v.ModifiedDate,
                ModifiedBy = v.ModifiedBy,
            });

            var pagedResult = await projectedQuery.GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<LookupValueListModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}