using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Models.PropertyModule;

namespace Harmony.Identity.Domain.Models.TenantModule;

/// <summary>A customer as the platform console sees it.</summary>
public class TenantModel
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string? ExternalIdentifier { get; set; }

    public string LegalNameEn { get; set; } = null!;

    public string LegalNameAr { get; set; } = null!;

    public string? CountryCode { get; set; }

    public string? DefaultCurrency { get; set; }

    public TenantStatus Status { get; set; }

    public string? StatusReason { get; set; }

    public int Tier { get; set; }

    public DateOnly OnboardedOn { get; set; }

    public DateOnly? ExportWindowEndsOn { get; set; }

    /// <summary>True for the platform's own row (us): the console shows it apart and offers no suspend.</summary>
    public bool IsPlatform { get; set; }

    public DateTime CreatedDate { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? ModifiedDate { get; set; }

    public string? ModifiedBy { get; set; }

    /// <summary>The customer's contract as lines, hotel lines and chain-wide ones. Filled on the detail read only.</summary>
    public List<SubscriptionLineModel> Subscriptions { get; set; } = [];
}
