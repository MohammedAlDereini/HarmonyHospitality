namespace Harmony.Identity.Domain.Models.RoleModule;

public class RoleDetailModel : RoleListModel
{
    public bool IsPrivileged { get; set; }

    public List<string> PermissionCodes { get; set; } = [];
}