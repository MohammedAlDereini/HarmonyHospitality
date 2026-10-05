using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.RegulatoryEnvironments.List
{
    public class GetPagedRegulatoryEnvironmentsQueryHandler : IRequestHandler<GetPagedRegulatoryEnvironmentsQuery, CallResponse<PagedResult<RegulatoryEnvironmentModel>>>
    {
        private readonly IRegulatoryEnvironmentRepository repository;

        public GetPagedRegulatoryEnvironmentsQueryHandler(IRegulatoryEnvironmentRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<RegulatoryEnvironmentModel>>> Handle(GetPagedRegulatoryEnvironmentsQuery request, CancellationToken cancellationToken)
        {
            var country = request.CountryCode?.Trim().ToUpperInvariant();
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(!string.IsNullOrEmpty(country), e => e.CountryCode == country);
            query = query.WhereIf(request.Kind.HasValue, e => e.Kind == request.Kind!.Value);
            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.RegulatoryEnvironment).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<RegulatoryEnvironmentModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
