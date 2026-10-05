using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Currencies.List
{
    public class GetPagedCurrenciesQueryHandler : IRequestHandler<GetPagedCurrenciesQuery, CallResponse<PagedResult<CurrencyModel>>>
    {
        private readonly ICurrencyRepository repository;

        public GetPagedCurrenciesQueryHandler(ICurrencyRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<CurrencyModel>>> Handle(GetPagedCurrenciesQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(request.IsActive.HasValue, e => e.IsActive == request.IsActive!.Value);
            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.Currency).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<CurrencyModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
