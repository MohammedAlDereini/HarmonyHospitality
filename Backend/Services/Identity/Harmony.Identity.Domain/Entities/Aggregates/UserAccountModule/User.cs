using Harmony.Core.Errors;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Core.Exceptions;
using Harmony.Core.Models;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Microsoft.AspNetCore.Identity;

namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// A person who can hold a Harmony token, stored by ASP.NET Core Identity. Identity owns the credential side
/// (password, lockout, 2FA, passkeys, external logins); Harmony adds who the account is: tenant, display name,
/// administrative state, roles, and SecurityVersion, which only ever increases: a bump kills every token minted before it.
/// </summary>
public sealed class User : IdentityUser<Guid>, IBaseEntity, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    private User()
    {
    }

    public string DisplayName { get; private set; } = null!;
    public AdministrativeState State { get; private set; }
    public string? StateReason { get; private set; }
    public int SecurityVersion { get; private set; }

    /// <summary>True until the person replaces a password somebody else chose (the first-account seed, an admin). While true, a token can only change the password.</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>How the second step is proved at sign-in, once two-step sign-in is on (TwoFactorEnabled, Identity's).</summary>
    public TwoStepMethod TwoStepMethod { get; private set; } = TwoStepMethod.AuthenticatorApp;

    /// <summary>
    /// Why an admin put this person on the two-step exception list (like Mews), or null when they are not on it. On the list,
    /// a person without two-step signs in with the password alone; it never applies to a Manager or higher role.
    /// </summary>
    public string? TwoStepExemptReason { get; private set; }

    /// <summary>
    /// Where this account was made and its id there (the ERD's Issuer, Subject): "local" and the account's own id for
    /// every account Harmony makes. A company sign-in is a link (ExternalIdentityLinks), never a change here.
    /// </summary>
    public string Issuer { get; private set; } = FederatedAuthority.LocalIssuer;

    public string Subject { get; private set; } = null!;

    /// <summary>A machine, not a person (the ERD's IsServicePrincipal). None exist yet; every account Harmony makes is a person.</summary>
    public bool IsServicePrincipal { get; private set; }

    /// <summary>Two-step is set up (the ERD's IsMfaEnrolled), computed by SQL from Identity's TwoFactorEnabled: never written here.</summary>
    public bool IsMfaEnrolled { get; private set; }

    /// <summary>The account this one was merged into (the ERD's SupersededByUserAccountId); null while it stands on its own. Set by stage D's merge.</summary>
    public Guid? SupersededByUserAccountId { get; private set; }

    /// <summary>
    /// The administrator kept this person on a Harmony password even where the customer's company login covers their
    /// e-mail domain (an exception, or the fallback administrator). False: the company login decides when it is on.
    /// </summary>
    public bool PasswordSignIn { get; private set; }

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    /// <summary>Roles at one hotel or one group. Changed only through AssignRole / EndAssignment.</summary>
    public ICollection<RoleAssignment> RoleAssignments { get; private set; } = new List<RoleAssignment>();

    /// <summary>Temporary elevations. Changed only through Grant / RevokeGrant.</summary>
    public ICollection<AccessGrant> AccessGrants { get; private set; } = new List<AccessGrant>();

    public bool CanSignIn => State == AdministrativeState.Active;

    public bool IsTwoStepExempt => TwoStepExemptReason is not null;

    public Guid TenantId { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime? ModifiedDate { get; private set; }
    public string ModifiedBy { get; private set; } = null!;

    public bool IsTransient() => Id == Guid.Empty;

    /// <summary>A person who may sign in. The password is set by Identity (UserManager) at creation, never here.</summary>
    public static User Create(string displayName, string userName, string email)
    {
        var id = Guid.CreateVersion7();
        return new User
        {
            Id = id,
            Subject = id.ToString("D"),
            UserName = userName,
            Email = email,
            DisplayName = IdentityText.DisplayName(displayName),
            State = AdministrativeState.Active,
            SecurityVersion = 1,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            LockoutEnabled = true,
        };
    }

    /// <summary>The administrator keeps this person on a Harmony password, whatever the company login covers.</summary>
    public void KeepPasswordSignIn() => PasswordSignIn = true;

    /// <summary>The person follows the customer's company login again (when it covers their e-mail and is on).</summary>
    public void FollowCompanySignIn() => PasswordSignIn = false;

    /// <summary>The way the person chose to prove the second step. Our code passes the value, never a request, so an unknown one is a bug.</summary>
    public void UseTwoStepMethod(TwoStepMethod method)
    {
        TwoStepMethod = Enum.IsDefined(method) ? method : throw new ArgumentOutOfRangeException(nameof(method));
    }

    /// <summary>
    /// On the two-step exception list, for a person who cannot use an app or a mailbox at work. Lowers nothing that is on:
    /// with two-step already turned on, the person still proves it. Exempting again only replaces the reason.
    /// </summary>
    public void ExemptFromTwoStep(string reason)
    {
        AssertNotTerminated();

        TwoStepExemptReason = IdentityText.Reason(reason) ?? throw new ArgumentException("A reason is required.", nameof(reason));
    }

    /// <summary>Off the exception list. The version bumps, so every password-only session ends and the next sign-in needs two-step.</summary>
    public void EndTwoStepExemption()
    {
        AssertNotTerminated();

        if (TwoStepExemptReason is null)
        {
            return;
        }

        TwoStepExemptReason = null;
        BumpSecurityVersion();
    }

    /// <summary>The password was chosen by someone else; the person must replace it before doing anything else. For new accounts, so nothing to kill.</summary>
    public void RequirePasswordChange()
    {
        AssertNotTerminated();

        MustChangePassword = true;
    }

    /// <summary>
    /// The domain side of a password change; Identity stores the hash. The must-change flag clears and the version bumps:
    /// a changed password logs the account out everywhere, the caller included, and no refresh token survives it.
    /// </summary>
    public void MarkPasswordChanged()
    {
        AssertNotTerminated();

        MustChangePassword = false;
        BumpSecurityVersion();
    }

    /// <summary>Logout everywhere: the version bumps, so every token on every device dies and no refresh token survives.</summary>
    public void EndAllSessions()
    {
        AssertNotTerminated();

        BumpSecurityVersion();
    }

    /// <summary>Blocks sign-in and kills current tokens. Suspending twice is a no-op.</summary>
    public void Suspend(string reason)
    {
        AssertNotTerminated();

        if (State == AdministrativeState.Suspended)
        {
            return;
        }

        State = AdministrativeState.Suspended;
        StateReason = IdentityText.Reason(reason);
        BumpSecurityVersion();
    }

    /// <summary>Back to Active. Nothing to kill, so the version stays.</summary>
    public void Reinstate()
    {
        AssertNotTerminated();

        State = AdministrativeState.Active;
        StateReason = null;
    }

    /// <summary>Final. The credential is destroyed and current tokens die. Terminating twice is a no-op.</summary>
    public void Terminate(string reason)
    {
        if (State == AdministrativeState.Terminated)
        {
            return;
        }

        State = AdministrativeState.Terminated;
        StateReason = IdentityText.Reason(reason);
        PasswordHash = null;
        BumpSecurityVersion();
    }

    /// <summary>The account holds exactly these roles afterwards: missing ones are added, others removed.</summary>
    public void UpdateRoles(IEnumerable<Guid>? roleIds)
    {
        AssertNotTerminated();

        var wanted = (roleIds ?? []).Distinct().ToList();
        var current = UserRoles.Select(ur => ur.RoleId).ToList();

        foreach (var roleId in wanted.Except(current))
        {
            UserRoles.Add(UserRole.New(roleId));
        }

        var changed = wanted.Except(current).Any() || current.Except(wanted).Any();

        foreach (var userRole in UserRoles.Where(ur => !wanted.Contains(ur.RoleId)).ToList())
        {
            UserRoles.Remove(userRole);
        }

        // Roles travel inside the token: a removed role must kill the tokens that still carry it.
        if (changed)
        {
            BumpSecurityVersion();
        }
    }

    /// <summary>A role at a hotel or a group. One current assignment per role and scope: a second one is refused (00105 by the handler first).</summary>
    public RoleAssignment AssignRole(Guid roleId, AssignmentScope scope, DateTimeOffset? endsAtUtc, DateTimeOffset utcNow)
    {
        AssertNotTerminated();
        if (RoleAssignments.Any(a => a.RoleId == roleId && a.Scope == scope && a.IsCurrentAt(utcNow)))
        {
            throw new InvalidOperationException("The account already holds this role at this scope.");
        }

        var assignment = RoleAssignment.New(TenantId, roleId, scope, endsAtUtc);
        RoleAssignments.Add(assignment);
        return assignment;
    }

    /// <summary>Ends an assignment now. Null when the id is not this account's; the assignment itself refuses a second ending.</summary>
    public RoleAssignment? EndAssignment(Guid assignmentId, DateTimeOffset utcNow)
    {
        var assignment = RoleAssignments.FirstOrDefault(a => a.Id == assignmentId);
        assignment?.End(utcNow);
        return assignment;
    }

    public AccessGrant Grant(Guid roleId, AssignmentScope scope, DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid grantedById, string? reason)
    {
        AssertNotTerminated();
        var grant = AccessGrant.New(TenantId, roleId, scope, fromUtc, toUtc, grantedById, reason);
        AccessGrants.Add(grant);
        return grant;
    }

    /// <summary>Revokes a grant now. Null when the id is not this account's; the grant itself refuses a second ending.</summary>
    public AccessGrant? RevokeGrant(Guid grantId, DateTimeOffset utcNow)
    {
        var grant = AccessGrants.FirstOrDefault(g => g.Id == grantId);
        grant?.Revoke(utcNow);
        return grant;
    }

    // The one rule the revocation design rests on: it never goes down. Identity's own kill switch,
    // the SecurityStamp, changes with it so both views of "this token is stale" agree.
    private void BumpSecurityVersion()
    {
        SecurityVersion++;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    private void AssertNotTerminated()
    {
        if (State == AdministrativeState.Terminated)
        {
            throw new BusinessException($"'{DisplayName}' is terminated.", Error.New(BusinessErrorCodes.Identity.UserAccount.Terminated));
        }
    }
}
