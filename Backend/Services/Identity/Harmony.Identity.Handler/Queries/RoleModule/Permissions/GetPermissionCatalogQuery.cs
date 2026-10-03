using Harmony.Identity.Domain.Models.RoleModule;

namespace Harmony.Identity.Handler.Queries.RoleModule.Permissions
{
    /// <summary>The permissions of the caller's tenant: what a role can be given.</summary>
    public class GetPermissionCatalogQuery : BaseQueryRequest<CallResponse<List<PermissionModel>>>
    {
    }
}
