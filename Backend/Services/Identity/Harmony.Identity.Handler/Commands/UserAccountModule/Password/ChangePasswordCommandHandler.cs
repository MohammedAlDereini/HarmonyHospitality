namespace Harmony.Identity.Handler.Commands.UserAccountModule.Password;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The caller changes their own password. The account is the token's subject, never a body field, so nobody can
/// change another person's password here. The current password must match (a wrong one counts towards the login
/// lockout, same rule as sign-in), the new one must differ and pass Identity's password rules. A success clears
/// MustChangePassword and bumps SecurityVersion: every token the person holds, the caller's own included, dies at
/// its next request and no refresh token survives, so the person signs in again with the new password.
/// </summary>
public class ChangePasswordCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<ChangePasswordCommand, CallResponse>
{
    private readonly ICallContext callContext;
    private readonly ILogger<ChangePasswordCommandHandler> logger;

    public ChangePasswordCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionRevoker sessionRevoker, ICallContext callContext, ILogger<ChangePasswordCommandHandler> logger)
        : base(userManager, cacheService, sessionRevoker)
    {
        this.callContext = callContext;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        // The framework copied the token's sub into the call context; anything else is not a person's token.
        if (!Guid.TryParse(this.callContext.UserId(), out var userId))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        var user = await this.LoadAsync(userId);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        // Same door as sign-in: a locked-out account gets no password check at all, so the lockout cannot be bypassed here.
        if (await this.UserManager.IsLockedOutAsync(user))
        {
            this.logger.LogWarning("Password change refused: account {UserId} is locked out.", user.Id);
            return Fail(BusinessErrorCodes.Identity.UserAccount.LockedOut);
        }

        if (!await this.UserManager.CheckPasswordAsync(user, command.CurrentPassword))
        {
            await this.UserManager.AccessFailedAsync(user);
            this.logger.LogWarning("Password change refused: wrong current password for account {UserId}.", user.Id);
            return Fail(BusinessErrorCodes.Identity.UserAccount.CurrentPasswordWrong);
        }

        if (await this.UserManager.CheckPasswordAsync(user, command.NewPassword))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NewPasswordSameAsCurrent);
        }

        // Domain first, store second: ChangePasswordAsync saves the whole entity, so the cleared flag and the
        // bumped version land in the same write as the new hash, or not at all.
        user.MarkPasswordChanged();
        var result = await this.UserManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!result.Succeeded)
        {
            // Only Identity's password rules can still say no here (digit, upper and lower case, length).
            this.logger.LogInformation("Password change refused for account {UserId}: {Codes}.", user.Id, string.Join(", ", result.Errors.Select(e => e.Code)));
            return Fail(BusinessErrorCodes.Identity.UserAccount.PasswordRejected);
        }

        await this.UserManager.ResetAccessFailedCountAsync(user);
        await this.PublishSecurityVersionAsync(user, cancellationToken);
        this.logger.LogInformation("Password changed for account {UserId}; SecurityVersion is now {Version}.", user.Id, user.SecurityVersion);

        return Ok();
    }
}
