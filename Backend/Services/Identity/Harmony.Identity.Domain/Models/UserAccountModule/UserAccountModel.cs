using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>An account as the API shows it. No password hash, no digest, no secret: those never leave the row.</summary>
public class UserAccountModel
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = null!;
    public string? Email { get; set; }
    public string DisplayName { get; set; } = null!;
    public bool TwoFactorEnabled { get; set; }
    /// <summary>Why the person is on the two-step exception list; null when they are not.</summary>
    public string? TwoStepExemptReason { get; set; }
    /// <summary>The roles the account holds; the person's standing is the highest privilege level among them.</summary>
    public List<Guid> RoleIds { get; set; } = new();
    public AdministrativeState State { get; set; }
    public string? StateReason { get; set; }
    /// <summary>Stage E: the company directory owns this person (name, state and roles change only there).</summary>
    public bool IsDirectoryManaged { get; set; }
    public int SecurityVersion { get; set; }
    /// <summary>The newest sign-in Identity recorded; null when the person never signed in.</summary>
    public DateTimeOffset? LastSignInUtc { get; set; }
    /// <summary>The device seen last ("Windows · Edge 130"); null when none was.</summary>
    public string? LastSignInDevice { get; set; }
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
}
