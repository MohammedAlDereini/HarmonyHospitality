namespace Harmony.Identity.Handler.Commands.UserAccountModule.Password;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The one password-change sequence, behind two doors: the API command (the caller is the token's subject) and the
/// sign-in walk's command (the person just proved who they are on the pages). The current password must match (a
/// wrong one counts towards the login lockout, same rule as sign-in), the new one must differ and pass Identity's
/// password rules. A success clears MustChangePassword and bumps SecurityVersion: every token the person holds dies at
/// its next request and no refresh token survives.
/// </summary>
public abstract class PasswordChangeHandlerBase : UserAccountCommandHandlerBase
{
    private readonly IAccountMailer mailer;
    private readonly ILogger logger;

    protected PasswordChangeHandlerBase(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IAccountMailer mailer, ILogger logger)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.mailer = mailer;
        this.logger = logger;
    }

    protected async Task<CallResponse> ChangeAsync(User user, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        // Same door as sign-in: a locked-out account gets no password check at all, so the lockout cannot be bypassed here.
        if (await this.UserManager.IsLockedOutAsync(user))
        {
            this.logger.LogWarning("Password change refused: account {UserId} is locked out.", user.Id);
            return Fail(BusinessErrorCodes.Identity.UserAccount.LockedOut);
        }

        if (!await this.UserManager.CheckPasswordAsync(user, currentPassword))
        {
            await this.UserManager.AccessFailedAsync(user);
            this.logger.LogWarning("Password change refused: wrong current password for account {UserId}.", user.Id);
            return Fail(BusinessErrorCodes.Identity.UserAccount.CurrentPasswordWrong);
        }

        if (await this.UserManager.CheckPasswordAsync(user, newPassword))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NewPasswordSameAsCurrent);
        }

        // Domain first, store second: ChangePasswordAsync saves the whole entity, so the cleared flag and the
        // bumped version land in the same write as the new hash, or not at all.
        user.MarkPasswordChanged();
        await this.TrailAsync(user, SecurityAuditActions.PasswordChanged, cancellationToken);
        var result = await this.UserManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            // Only the password checks can still say no here: the length, the symbol, the blocklist (leaked, own name).
            this.logger.LogInformation("Password change refused for account {UserId}: {Codes}.", user.Id, string.Join(", ", result.Errors.Select(e => e.Code)));
            return Fail(PasswordRefusal(result.Errors.Select(e => e.Code)));
        }

        await this.UserManager.ResetAccessFailedCountAsync(user);
        await this.PublishSecurityVersionAsync(user, cancellationToken);
        this.logger.LogInformation("Password changed for account {UserId}; SecurityVersion is now {Version}.", user.Id, user.SecurityVersion);
        await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.PasswordChanged, cancellationToken);

        return Ok();
    }
}
