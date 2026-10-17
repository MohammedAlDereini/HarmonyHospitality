using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Modules.List
{
    public class GetPagedModulesQueryHandler : IRequestHandler<GetPagedModulesQuery, CallResponse<PagedResult<ModuleModel>>>
    {
        private readonly IModuleRepository repository;

        public GetPagedModulesQueryHandler(IModuleRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<PagedResult<ModuleModel>>> Handle(GetPagedModulesQuery request, CancellationToken cancellationToken)
        {
            var owner = request.OwnerService?.Trim();
            var keyword = request.SearchText?.Trim();

            var query = this.repository.Query();

            query = query.WhereIf(!string.IsNullOrEmpty(owner), e => e.OwnerService == owner);
            query = query.WhereIf(request.Level.HasValue, e => e.Level == request.Level!.Value);
            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!));

            var pagedResult = await query.Select(ReferenceProjections.Module).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<ModuleModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
