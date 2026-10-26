namespace Harmony.Identity.Domain.Models.PropertyGroupModule;

/// <summary>A hotel group as the console and the hotel's own people see it.</summary>
public class PropertyGroupModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string TypeCode { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string? ExternalIdentifier { get; set; }

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string? Description { get; set; }

    public int Precedence { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Hotels in the group today (open periods). On the list and the detail.</summary>
    public int CurrentMembers { get; set; }

    /// <summary>The brand's own columns; null unless the type is Brand and a detail was given. Detail only.</summary>
    public BrandDetailModel? Brand { get; set; }

    /// <summary>Every membership, current first, then by hotel code. Detail only.</summary>
    public List<PropertyGroupMemberModel> Members { get; set; } = [];

    public DateTime CreatedDate { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? ModifiedDate { get; set; }

    public string? ModifiedBy { get; set; }
}
