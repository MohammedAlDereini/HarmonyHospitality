using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Core.Exceptions;
using Harmony.Core.Models;
using Harmony.Identity.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// A person or a machine that can hold a Harmony token, stored by ASP.NET Core Identity.
/// Identity owns the credential side (password, lockout, 2FA, passkeys, external logins);
/// Harmony adds who the account is: tenant, display name, administrative state, and
/// SecurityVersion, which only ever increases — a bump kills every token minted before it.
/// </summary>
public sealed class HarmonyUser : IdentityUser<Guid>, IBaseEntity, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    private HarmonyUser()
    {
    }

    public string DisplayName { get; private set; } = null!;
    public bool IsServicePrincipal { get; private set; }
    public AdministrativeState State { get; private set; }
    public string? StateReason { get; private set; }
    public int SecurityVersion { get; private set; }

    /// <summary>SHA-256 of the current service secret, hex. Never the secret. Null for people and after termination.</summary>
    public string? ServiceCredentialDigest { get; private set; }
    public DateTime? ServiceCredentialIssuedOn { get; private set; }

    public bool CanSignIn => State == AdministrativeState.Active;

    public Guid TenantId { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime? ModifiedDate { get; private set; }
    public string ModifiedBy { get; private set; } = null!;

    public bool IsTransient() => Id == Guid.Empty;

    /// <summary>A machine account: its code is the user name, it has no password, and its secret is returned exactly once.</summary>
    public static HarmonyUser CreateServicePrincipal(string code, string displayName, DateTime nowUtc, out string secret)
    {
        var user = new HarmonyUser
        {
            Id = Uid.New(),
            UserName = IdentityText.ServicePrincipalCode(code),
            DisplayName = IdentityText.DisplayName(displayName),
            IsServicePrincipal = true,
            State = AdministrativeState.Active,
            SecurityVersion = 1,
        };

        secret = user.IssueSecret(nowUtc);
        return user;
    }

    /// <summary>A new secret; the old one stops working at once, and every token minted so far dies with it.</summary>
    public string RotateServiceCredential(DateTime nowUtc)
    {
        AssertServicePrincipal();
        AssertNotTerminated();

        var secret = IssueSecret(nowUtc);
        BumpSecurityVersion();
        return secret;
    }

    public bool VerifyServiceCredential(string? secret)
        => IsServicePrincipal && CanSignIn && ServiceSecret.Matches(secret, ServiceCredentialDigest);

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
        ServiceCredentialDigest = null;
        PasswordHash = null;
        BumpSecurityVersion();
    }

    private string IssueSecret(DateTime nowUtc)
    {
        var secret = ServiceSecret.Generate();
        ServiceCredentialDigest = ServiceSecret.Digest(secret);
        ServiceCredentialIssuedOn = nowUtc;
        return secret;
    }

    // The one rule the revocation design rests on: it never goes down. Identity's own kill switch,
    // the SecurityStamp, changes with it so both views of "this token is stale" agree.
    private void BumpSecurityVersion()
    {
        SecurityVersion++;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    private void AssertServicePrincipal()
    {
        if (!IsServicePrincipal)
        {
            throw new BusinessException($"'{DisplayName}' is not a service principal.", Error.New(IdentityErrorCodes.NotAServicePrincipal));
        }
    }

    private void AssertNotTerminated()
    {
        if (State == AdministrativeState.Terminated)
        {
            throw new BusinessException($"'{DisplayName}' is terminated.", Error.New(IdentityErrorCodes.UserAccountTerminated));
        }
    }
}
