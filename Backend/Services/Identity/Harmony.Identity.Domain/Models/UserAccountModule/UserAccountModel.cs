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
    public AdministrativeState State { get; set; }
    public string? StateReason { get; set; }
    public int SecurityVersion { get; set; }
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
}
