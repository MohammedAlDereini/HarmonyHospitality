namespace Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>What gives an account access beyond its tenant-wide roles: its assignments and its grants, current first.</summary>
public class AccountAccessModel
{
    public Guid Id { get; set; }

    public List<RoleAssignmentModel> Assignments { get; set; } = [];

    public List<AccessGrantModel> Grants { get; set; } = [];
}

public class RoleAssignmentModel
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public string RoleCode { get; set; } = null!;

    public Guid? PropertyId { get; set; }

    public Guid? PropertyGroupId { get; set; }

    /// <summary>The hotel's or the group's code.</summary>
    public string ScopeCode { get; set; } = null!;

    public DateTimeOffset? EndsAtUtc { get; set; }

    public bool IsCurrent { get; set; }
}

public class AccessGrantModel
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public string RoleCode { get; set; } = null!;

    public Guid? PropertyId { get; set; }

    public Guid? PropertyGroupId { get; set; }

    public string ScopeCode { get; set; } = null!;

    public DateTimeOffset FromUtc { get; set; }

    public DateTimeOffset ToUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public Guid GrantedById { get; set; }

    public string Reason { get; set; } = null!;

    public bool IsActive { get; set; }
}
