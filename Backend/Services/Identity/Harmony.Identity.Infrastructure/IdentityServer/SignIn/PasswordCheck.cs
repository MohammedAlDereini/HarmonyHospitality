using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Harmony.Identity.Infrastructure.IdentityServer.SignIn;

public enum PasswordCheckStatus
{
    /// <summary>Password right, account active, no second factor: the person is signed in.</summary>
    Accepted,

    /// <summary>Password right, but the account has an authenticator: the sign-in waits for the code.</summary>
    SecondFactorRequired,

    /// <summary>Unknown name or wrong password: one answer for both, so nobody learns which names exist.</summary>
    InvalidCredentials,

    AccountNotActive,
    LockedOut,

    /// <summary>The same name exists in several tenants and the request did not say which.</summary>
    TenantRequired,
}

public sealed record PasswordCheckResult(PasswordCheckStatus Status, User? User);

/// <summary>
/// PropX LoginCommandHandler rules, in one place for every way a person signs in (the sign-in page and, until it is
/// removed, the password grant): the account exists, is Active, is not locked out, and the password matches.
/// Identity's lockout counts the failures (5 in a row lock the account for 5 minutes). Sign-in runs before any tenant is
/// known, so the lookup searches every tenant; a tenant id picks the account when the same name exists in several.
/// </summary>
public sealed class PasswordCheck
{
    private readonly IUserAccountRepository userAccounts;
    private readonly UserManager<User> userManager;
    private readonly ILogger<PasswordCheck> logger;

    public PasswordCheck(IUserAccountRepository userAccounts, UserManager<User> userManager, ILogger<PasswordCheck> logger)
    {
        this.userAccounts = userAccounts;
        this.userManager = userManager;
        this.logger = logger;
    }

    public async Task<PasswordCheckResult> CheckAsync(string? userName, string? password, string? tenant, CancellationToken cancellationToken)
    {
        var name = this.userManager.NormalizeName(userName?.Trim() ?? string.Empty);

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password))
        {
            return new(PasswordCheckStatus.InvalidCredentials, null);
        }

        var candidates = await this.userAccounts.FindByUserNameAcrossTenantsAsync(name, cancellationToken);

        var hasTenant = Guid.TryParse(tenant, out var tenantId);
        if (candidates.Count > 1 && !hasTenant)
        {
            return new(PasswordCheckStatus.TenantRequired, null);
        }

        var user = hasTenant
            ? candidates.SingleOrDefault(u => u.TenantId == tenantId)
            : candidates.SingleOrDefault();

        if (user is null)
        {
            this.logger.LogInformation("Sign-in refused: unknown account.");
            return new(PasswordCheckStatus.InvalidCredentials, null);
        }

        if (!user.CanSignIn)
        {
            this.logger.LogInformation("Sign-in refused: account {UserId} is {State}.", user.Id, user.State);
            return new(PasswordCheckStatus.AccountNotActive, user);
        }

        if (await this.userManager.IsLockedOutAsync(user))
        {
            this.logger.LogWarning("Sign-in refused: account {UserId} is locked out.", user.Id);
            return new(PasswordCheckStatus.LockedOut, user);
        }

        if (!await this.userManager.CheckPasswordAsync(user, password))
        {
            // Identity counts the failure; the fifth in a row locks the account for the configured window.
            await this.userManager.AccessFailedAsync(user);
            var locked = await this.userManager.IsLockedOutAsync(user);
            this.logger.LogInformation("Sign-in refused: wrong password for account {UserId}. LockedOut={LockedOut}", user.Id, locked);
            return new(locked ? PasswordCheckStatus.LockedOut : PasswordCheckStatus.InvalidCredentials, user);
        }

        await this.userManager.ResetAccessFailedCountAsync(user);

        if (user.TwoFactorEnabled)
        {
            this.logger.LogInformation("Sign-in needs a second factor for account {UserId}.", user.Id);
            return new(PasswordCheckStatus.SecondFactorRequired, user);
        }

        this.logger.LogInformation("Sign-in accepted for account {UserId} in tenant {Tenant}.", user.Id, user.TenantId);
        return new(PasswordCheckStatus.Accepted, user);
    }
}