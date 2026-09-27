using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

namespace Harmony.Identity.Domain.Models.RoleModule;

public class RoleListModel
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string? NameAr { get; set; }

    public PrivilegeLevel PrivilegeLevel { get; set; }

    public bool IsSystemRole { get; set; }

    public bool IsSuperRole { get; set; }

    public DateTime CreatedDate { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? ModifiedDate { get; set; }

    public string? ModifiedBy { get; set; }
}