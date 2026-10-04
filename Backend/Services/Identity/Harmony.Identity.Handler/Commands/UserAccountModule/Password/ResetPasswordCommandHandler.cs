namespace Harmony.Identity.Handler.Commands.UserAccountModule.Password;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The link was opened: Identity checks the one-time token against the account's security stamp and stores the new
/// password (an account created by invitation gets its first password here). Then the same consequences as any password
/// change: MustChangePassword cleared, SecurityVersion bumped, every session on every device ended, lockout counter reset.
/// Unknown address, wrong or used token and expired token all answer the same code, so nothing can be probed.
/// </summary>
public class ResetPasswordCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<ResetPasswordCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly ILogger<ResetPasswordCommandHandler> logger;

    public ResetPasswordCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionRevoker sessionRevoker, IUserAccountRepository userAccounts, ILogger<ResetPasswordCommandHandler> logger)
        : base(userManager, cacheService, sessionRevoker)
    {
        this.userAccounts = userAccounts;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        // Anonymous call: no tenant is known, so the lookup runs across tenants and must hit exactly one account.
        var normalized = this.UserManager.NormalizeName(command.Email.Trim());
        var candidates = await this.userAccounts.FindByUserNameAcrossTenantsAsync(normalized, cancellationToken);

        if (candidates.Count != 1 || candidates[0].State == AdministrativeState.Terminated)
        {
            this.logger.LogInformation("Password reset refused: no single live account for the address.");
            return Fail(BusinessErrorCodes.Identity.UserAccount.ResetTokenInvalid);
        }

        var user = candidates[0];

        // The token is bound to the security stamp, so it is checked BEFORE anything rotates the stamp.
        var result = await this.UserManager.ResetPasswordAsync(user, command.Token, command.NewPassword);
        if (!result.Succeeded)
        {
            var codes = result.Errors.Select(e => e.Code).ToList();
            this.logger.LogInformation("Password reset refused for account {UserId}: {Codes}.", user.Id, string.Join(", ", codes));

            if (codes.Contains("InvalidToken"))
            {
                return Fail(BusinessErrorCodes.Identity.UserAccount.ResetTokenInvalid);
            }

            if (codes.Any(code => code.StartsWith("Password", StringComparison.Ordinal)))
            {
                return Fail(BusinessErrorCodes.Identity.UserAccount.PasswordRejected);
            }

            return Fail(BusinessErrorCodes.Identity.UserAccount.StoreRefused);
        }

        // The hash is stored; now the domain side of a password change, same as ChangePassword.
        user.MarkPasswordChanged();
        await this.SaveAsync(user);
        await this.UserManager.ResetAccessFailedCountAsync(user);
        await this.PublishSecurityVersionAsync(user, cancellationToken);
        this.logger.LogInformation("Password set through a link for account {UserId}; SecurityVersion is now {Version}.", user.Id, user.SecurityVersion);

        return Ok();
    }
}
