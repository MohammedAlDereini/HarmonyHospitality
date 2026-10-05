using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.CountrySubdivisions.List
{
    public class GetPagedCountrySubdivisionsQueryHandler : IRequestHandler<GetPagedCountrySubdivisionsQuery, CallResponse<PagedResult<CountrySubdivisionModel>>>
    {
        private readonly ICountrySubdivisionRepository repository;

        public GetPagedCountrySubdivisionsQueryHandler(ICountrySubdivisionRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<CountrySubdivisionModel>>> Handle(GetPagedCountrySubdivisionsQuery request, CancellationToken cancellationToken)
        {
            var country = request.CountryCode?.Trim().ToUpperInvariant();
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(!string.IsNullOrEmpty(country), e => e.CountryCode == country);
            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.CountrySubdivision).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<CountrySubdivisionModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
