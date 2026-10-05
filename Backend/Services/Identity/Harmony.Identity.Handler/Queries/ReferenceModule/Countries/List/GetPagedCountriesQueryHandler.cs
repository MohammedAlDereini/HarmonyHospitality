using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Countries.List
{
    public class GetPagedCountriesQueryHandler : IRequestHandler<GetPagedCountriesQuery, CallResponse<PagedResult<CountryModel>>>
    {
        private readonly ICountryRepository repository;

        public GetPagedCountriesQueryHandler(ICountryRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<CountryModel>>> Handle(GetPagedCountriesQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(request.IsActive.HasValue, e => e.IsActive == request.IsActive!.Value);
            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.Code.Contains(keyword!) || e.Alpha3.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.Country).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<CountryModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
