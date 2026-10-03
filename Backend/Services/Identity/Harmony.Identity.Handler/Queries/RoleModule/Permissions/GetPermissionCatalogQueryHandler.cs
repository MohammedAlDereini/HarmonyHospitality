using Harmony.Identity.Domain.Models.RoleModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.RoleModule.Permissions
{
    public class GetPermissionCatalogQueryHandler : IRequestHandler<GetPermissionCatalogQuery, CallResponse<List<PermissionModel>>>
    {
        private readonly IPermissionRepository permissions;

        public GetPermissionCatalogQueryHandler(IPermissionRepository permissions)
        {
            this.permissions = permissions;
        }

        public async Task<CallResponse<List<PermissionModel>>> Handle(GetPermissionCatalogQuery request, CancellationToken cancellationToken)
        {
            var list = await this.permissions.Query()
                .OrderBy(p => p.PermissionValue)
                .Select(p => new PermissionModel
                {
                    Id = p.Id,
                    PermissionValue = p.PermissionValue,
                    Name = p.Name,
                    Description = p.Description,
                    EndPoint = p.EndPoint,
                    IsActive = p.IsActive,
                })
                .ToListAsync(cancellationToken);

            return CallResponseBuilder.CreateResponse<List<PermissionModel>>(eCallResponseStatus.Success).HasData(list);
        }
    }
}
