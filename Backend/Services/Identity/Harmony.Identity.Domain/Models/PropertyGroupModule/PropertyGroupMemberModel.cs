namespace Harmony.Identity.Domain.Models.PropertyGroupModule;

/// <summary>One hotel's membership as the detail read shows it, current ones first.</summary>
public class PropertyGroupMemberModel
{
    public Guid Id { get; set; }

    public Guid PropertyId { get; set; }

    public string PropertyCode { get; set; } = null!;

    public string PropertyNameEn { get; set; } = null!;

    public string PropertyNameAr { get; set; } = null!;

    public DateTimeOffset FromUtc { get; set; }

    public DateTimeOffset? ToUtc { get; set; }

    public bool IsCurrent { get; set; }
}
