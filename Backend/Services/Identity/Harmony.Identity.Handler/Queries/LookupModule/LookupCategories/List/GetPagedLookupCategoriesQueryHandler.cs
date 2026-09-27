using Harmony.Identity.Domain.Models.LookupModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.LookupModule.LookupCategories.List
{
    public class GetPagedLookupCategoriesQueryHandler : IRequestHandler<GetPagedLookupCategoriesQuery, CallResponse<PagedResult<LookupCategoryListModel>>>
    {
        private readonly ILookupCategoryRepository lookupCategoryRepository;

        public GetPagedLookupCategoriesQueryHandler(ILookupCategoryRepository lookupCategoryRepository)
        {
            this.lookupCategoryRepository = lookupCategoryRepository;
        }

        public async Task<CallResponse<PagedResult<LookupCategoryListModel>>> Handle(GetPagedLookupCategoriesQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim()?.ToLower();

            var query = this.lookupCategoryRepository.Query();

            query = query.WhereIf(request.SearchText.IsNotNullOrEmpty() && request.SearchText!.Length >= 2, c => c.Name.Contains(keyword!));

            var projectedQuery = query.Select(c => new LookupCategoryListModel
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsGlobal = c.IsGlobal,
                CreatedDate = c.CreatedDate,
                CreatedBy = c.CreatedBy,
                ModifiedDate = c.ModifiedDate,
                ModifiedBy = c.ModifiedBy,
            });

            var pagedResult = await projectedQuery.GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<LookupCategoryListModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}