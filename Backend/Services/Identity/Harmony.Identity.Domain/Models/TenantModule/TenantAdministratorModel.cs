using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Models.TenantModule;

/// <summary>
/// The account holding a customer's ADMINISTRATOR role, as the console's tenant list shows it (one line: who, and
/// whether the invitation was taken up). The invitation is Identity's own LocalStaffEnrolment; a customer whose
/// administrator signs in through the company has none.
/// </summary>
public class TenantAdministratorModel
{
    public Guid UserAccountId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string? Email { get; set; }

    public AdministrativeState State { get; set; }

    /// <summary>The newest invitation is still open: the person has not set a first password yet.</summary>
    public bool InvitationPending { get; set; }

    /// <summary>The newest invitation ran out unredeemed: InviteAdministrator sends a new one.</summary>
    public bool InvitationExpired { get; set; }

    /// <summary>When the newest invitation went out; null when the account was never invited.</summary>
    public DateTime? InvitedOn { get; set; }

    /// <summary>Two-step sign-in is set up on the account.</summary>
    public bool TwoStepEnrolled { get; set; }

    /// <summary>The account's second step, by name (Identity's TwoStepMethod).</summary>
    public string TwoStepMethod { get; set; } = null!;

    /// <summary>When the administrator last signed in, and how (the session's methods); null when never, or on the list.</summary>
    public DateTimeOffset? LastSignInUtc { get; set; }

    public string? LastSignInMethods { get; set; }
}
