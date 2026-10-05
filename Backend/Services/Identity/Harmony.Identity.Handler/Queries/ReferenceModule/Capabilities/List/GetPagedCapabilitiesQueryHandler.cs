using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Capabilities.List
{
    public class GetPagedCapabilitiesQueryHandler : IRequestHandler<GetPagedCapabilitiesQuery, CallResponse<PagedResult<CapabilityModel>>>
    {
        private readonly ICapabilityRepository repository;

        public GetPagedCapabilitiesQueryHandler(ICapabilityRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<CapabilityModel>>> Handle(GetPagedCapabilitiesQuery request, CancellationToken cancellationToken)
        {
            var owner = request.OwnerService?.Trim();
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(!string.IsNullOrEmpty(owner), e => e.OwnerService == owner);
            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.Capability).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<CapabilityModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
