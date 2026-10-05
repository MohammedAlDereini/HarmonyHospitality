using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.TimeZones.List
{
    public class GetPagedTimeZonesQueryHandler : IRequestHandler<GetPagedTimeZonesQuery, CallResponse<PagedResult<TimeZoneModel>>>
    {
        private readonly ITimeZoneRepository repository;

        public GetPagedTimeZonesQueryHandler(ITimeZoneRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<TimeZoneModel>>> Handle(GetPagedTimeZonesQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(request.IsActive.HasValue, e => e.IsActive == request.IsActive!.Value);
            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.Id.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.TimeZone).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<TimeZoneModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
