using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Languages.List
{
    public class GetPagedLanguagesQueryHandler : IRequestHandler<GetPagedLanguagesQuery, CallResponse<PagedResult<LanguageModel>>>
    {
        private readonly ILanguageRepository repository;

        public GetPagedLanguagesQueryHandler(ILanguageRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<LanguageModel>>> Handle(GetPagedLanguagesQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(request.IsActive.HasValue, e => e.IsActive == request.IsActive!.Value);
            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameNative.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.Language).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<LanguageModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
