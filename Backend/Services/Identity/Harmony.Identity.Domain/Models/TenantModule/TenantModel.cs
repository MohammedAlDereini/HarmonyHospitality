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

    /// <summary>Stage D: risky changes wait for a second administrator.</summary>
    public bool ApprovalsRequired { get; set; }

    /// <summary>Stage E: days without a sign-in before an account is suspended (1 to 90).</summary>
    public int InactivityDays { get; set; }

    /// <summary>A sign-in from an untrusted browser waits for the person's "Trust device" (L16).</summary>
    public bool TrustedDevicesRequired { get; set; }

    public DateTime CreatedDate { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? ModifiedDate { get; set; }

    public string? ModifiedBy { get; set; }

    /// <summary>The customer's contract as lines, hotel lines and chain-wide ones. Filled on the detail read only.</summary>
    public List<SubscriptionLineModel> Subscriptions { get; set; } = [];

    /// <summary>The customer's hotels, in every state (the list only).</summary>
    public int Hotels { get; set; }

    /// <summary>Rooms the customer's active hotels declare, summed (the list only).</summary>
    public int RoomsInUse { get; set; }

    /// <summary>Rooms licensed today: each hotel's largest held line, summed (the list only).</summary>
    public int LicensedRooms { get; set; }

    /// <summary>The module codes with an enabled line, each once, in order (the list only).</summary>
    public List<string> ModuleCodes { get; set; } = [];

    /// <summary>The customer's people, how many of them keep a password, how many are on the two-step exception list (the list and the detail).</summary>
    public int People { get; set; }

    public int PeopleWithPassword { get; set; }

    public int TwoStepExceptions { get; set; }

    /// <summary>The account holding the customer's ADMINISTRATOR role and where its invitation stands; null when there is none (the list and the detail).</summary>
    public TenantAdministratorModel? Administrator { get; set; }
}
