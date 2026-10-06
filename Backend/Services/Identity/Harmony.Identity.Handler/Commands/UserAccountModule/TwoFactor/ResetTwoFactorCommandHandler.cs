namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoFactor;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The admin door for a lost phone: two-factor goes off and the authenticator key is dropped, so the person signs in with
/// the password alone and sets the app up again. Because someone else just lowered the account's security, every session
/// of that account ends (SecurityVersion bump + refresh tokens removed). The admin cannot read or set any code here.
/// </summary>
public class ResetTwoFactorCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<ResetTwoFactorCommand, CallResponse>
{
    private readonly IAccountMailer mailer;
    private readonly ILogger<ResetTwoFactorCommandHandler> logger;

    public ResetTwoFactorCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionRevoker sessionRevoker, IAccountMailer mailer, ILogger<ResetTwoFactorCommandHandler> logger)
        : base(userManager, cacheService, sessionRevoker)
    {
        this.mailer = mailer;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(ResetTwoFactorCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (!user.TwoFactorEnabled)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.TwoFactorNotEnabled);
        }

        // Domain first, store second: SetTwoFactorEnabledAsync saves the whole entity, so the bump lands with the change.
        user.EndAllSessions();
        EnsureSucceeded(await this.UserManager.SetTwoFactorEnabledAsync(user, false));
        EnsureSucceeded(await this.UserManager.ResetAuthenticatorKeyAsync(user));

        await this.PublishSecurityVersionAsync(user, cancellationToken);
        this.logger.LogInformation("Two-factor reset by an admin for account {UserId}; SecurityVersion is now {Version}.", user.Id, user.SecurityVersion);
        await this.mailer.SendSecurityAlertAsync(user, SecurityAlert.TwoStepResetByAdmin, cancellationToken);

        return Ok();
    }
}
