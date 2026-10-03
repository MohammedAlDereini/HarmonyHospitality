using Harmony.Identity.Shared.Enums;

namespace Harmony.Identity.Domain.Models.RoleModule;

/// <summary>A permission row as the API shows it: what a role can be given.</summary>
public class PermissionModel
{
    public Guid Id { get; set; }

    public PermissionEnum PermissionValue { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? EndPoint { get; set; }

    public bool IsActive { get; set; }
}
