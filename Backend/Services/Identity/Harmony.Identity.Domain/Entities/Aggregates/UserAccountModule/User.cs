using Harmony.Core.Errors;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Core.Exceptions;
using Harmony.Core.Models;
using Harmony.Identity.Domain.Common;
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
    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    public bool CanSignIn => State == AdministrativeState.Active;

    public Guid TenantId { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime? ModifiedDate { get; private set; }
    public string ModifiedBy { get; private set; } = null!;

    public bool IsTransient() => Id == Guid.Empty;

    /// <summary>A person who may sign in. The password is set by Identity (UserManager) at creation, never here.</summary>
    public static User Create(string displayName, string userName, string email)
    {
        return new User
        {
            Id = Guid.CreateVersion7(),
            UserName = userName,
            Email = email,
            DisplayName = IdentityText.DisplayName(displayName),
            State = AdministrativeState.Active,
            SecurityVersion = 1,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            LockoutEnabled = true,
        };
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

    /// <summary>The account holds exactly these roles afterwards: missing ones are added, others removed. Same as PropX User.Update.</summary>
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
