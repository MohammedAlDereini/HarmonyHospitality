namespace Harmony.Identity.Domain.Models.LookupModule;

public class LookupValueListModel
{
    public Guid Id { get; set; }

    public Guid LookupCategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public bool IsGlobal { get; set; }

    public DateTime CreatedDate { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? ModifiedDate { get; set; }

    public string? ModifiedBy { get; set; }
}