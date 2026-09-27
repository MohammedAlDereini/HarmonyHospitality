using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

namespace Harmony.Identity.Handler.Queries.RoleModule.Permissions
{
    public class GetPermissionCatalogQueryHandler : IRequestHandler<GetPermissionCatalogQuery, CallResponse<List<string>>>
    {
        public Task<CallResponse<List<string>>> Handle(GetPermissionCatalogQuery request, CancellationToken cancellationToken)
        {
            var permissions = PermissionCatalog.All.Order().ToList();

            return Task.FromResult(CallResponseBuilder.CreateResponse<List<string>>(eCallResponseStatus.Success).HasData(permissions));
        }
    }
}