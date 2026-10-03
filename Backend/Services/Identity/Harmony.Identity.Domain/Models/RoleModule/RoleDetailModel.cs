namespace Harmony.Identity.Domain.Models.RoleModule;

public class RoleDetailModel : RoleListModel
{
    public bool IsPrivileged { get; set; }

    /// <summary>The permissions the role holds. For the super role: every active permission of the tenant.</summary>
    public List<PermissionModel> Permissions { get; set; } = [];
}
