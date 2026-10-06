namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoFactor;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The caller turns two-factor off after proving the password again (same lockout rules as sign-in: a locked account gets
/// no check, a wrong password counts). The authenticator key is dropped, so a later setup starts from a fresh key.
/// </summary>
public class DisableTwoFactorCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<DisableTwoFactorCommand, CallResponse>
{
    private readonly ICallContext callContext;
    private readonly IAccountMailer mailer;
    private readonly ILogger<DisableTwoFactorCommandHandler> logger;

    public DisableTwoFactorCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionRevoker sessionRevoker, ICallContext callContext, IAccountMailer mailer, ILogger<DisableTwoFactorCommandHandler> logger)
        : base(userManager, cacheService, sessionRevoker)
    {
        this.callContext = callContext;
        this.mailer = mailer;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(DisableTwoFactorCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.UserId(), out var userId))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        var user = await this.LoadAsync(userId);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (!user.TwoFactorEnabled)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.TwoFactorNotEnabled);
        }

        if (await this.UserManager.IsLockedOutAsync(user))
        {
            this.logger.LogWarning("Two-factor disable refused: account {UserId} is locked out.", user.Id);
            return Fail(BusinessErrorCodes.Identity.UserAccount.LockedOut);
        }

        if (!await this.UserManager.CheckPasswordAsync(user, command.CurrentPassword))
        {
            await this.UserManager.AccessFailedAsync(user);
            this.logger.LogWarning("Two-factor disable refused: wrong current password for account {UserId}.", user.Id);
            return Fail(BusinessErrorCodes.Identity.UserAccount.CurrentPasswordWrong);
        }

        EnsureSucceeded(await this.UserManager.SetTwoFactorEnabledAsync(user, false));
        EnsureSucceeded(await this.UserManager.ResetAuthenticatorKeyAsync(user));
        await this.UserManager.ResetAccessFailedCountAsync(user);
        this.logger.LogInformation("Two-factor disabled for account {UserId}.", user.Id);
        await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.TwoStepTurnedOff, cancellationToken);

        return Ok();
    }
}
