using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Cities.List
{
    public class GetPagedCitiesQueryHandler : IRequestHandler<GetPagedCitiesQuery, CallResponse<PagedResult<CityModel>>>
    {
        private readonly ICityRepository repository;

        public GetPagedCitiesQueryHandler(ICityRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<CityModel>>> Handle(GetPagedCitiesQuery request, CancellationToken cancellationToken)
        {
            var country = request.CountryCode?.Trim().ToUpperInvariant();
            var subdivision = request.SubdivisionCode?.Trim().ToUpperInvariant();
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(!string.IsNullOrEmpty(country), e => e.CountryCode == country);
            query = query.WhereIf(!string.IsNullOrEmpty(subdivision), e => e.SubdivisionCode == subdivision);
            query = query.WhereIf(request.IsActive.HasValue, e => e.IsActive == request.IsActive!.Value);
            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.City).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<CityModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
