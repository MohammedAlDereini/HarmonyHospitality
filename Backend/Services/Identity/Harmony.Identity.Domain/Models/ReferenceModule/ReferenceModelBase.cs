namespace Harmony.Identity.Domain.Models.ReferenceModule;

/// <summary>The audit trail every reference row carries.</summary>
public abstract class ReferenceModelBase
{
    public DateTime CreatedDate { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? ModifiedDate { get; set; }

    public string? ModifiedBy { get; set; }
}
