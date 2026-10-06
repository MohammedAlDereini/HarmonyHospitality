namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoFactor;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The caller proves the authenticator app holds the key by sending one current code. Then two-factor is on: every sign-in
/// needs the app (or a recovery code) after the password. Ten recovery codes are generated and returned exactly once; each
/// works one time. Current sessions stay valid: turning security up does not log anyone out.
/// </summary>
public class EnableTwoFactorCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<EnableTwoFactorCommand, CallResponse<TwoFactorRecoveryCodesModel>>
{
    public const int RecoveryCodeCount = 10;

    private readonly ICallContext callContext;
    private readonly ILogger<EnableTwoFactorCommandHandler> logger;

    public EnableTwoFactorCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionRevoker sessionRevoker, ICallContext callContext, ILogger<EnableTwoFactorCommandHandler> logger)
        : base(userManager, cacheService, sessionRevoker)
    {
        this.callContext = callContext;
        this.logger = logger;
    }

    public async Task<CallResponse<TwoFactorRecoveryCodesModel>> Handle(EnableTwoFactorCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.UserId(), out var userId))
        {
            return Fail<TwoFactorRecoveryCodesModel>(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        var user = await this.LoadAsync(userId);
        if (user is null)
        {
            return Fail<TwoFactorRecoveryCodesModel>(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (user.TwoFactorEnabled)
        {
            return Fail<TwoFactorRecoveryCodesModel>(BusinessErrorCodes.Identity.UserAccount.TwoFactorAlreadyEnabled);
        }

        if (string.IsNullOrEmpty(await this.UserManager.GetAuthenticatorKeyAsync(user)))
        {
            return Fail<TwoFactorRecoveryCodesModel>(BusinessErrorCodes.Identity.UserAccount.TwoFactorNotSetUp);
        }

        if (!await this.UserManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, command.Code))
        {
            this.logger.LogInformation("Two-factor setup not confirmed for account {UserId}: the code did not verify.", user.Id);
            return Fail<TwoFactorRecoveryCodesModel>(BusinessErrorCodes.Identity.UserAccount.TwoFactorCodeInvalid);
        }

        // This path is the authenticator app; saving the two-step flag saves the method with it.
        user.UseTwoStepMethod(TwoStepMethod.AuthenticatorApp);
        EnsureSucceeded(await this.UserManager.SetTwoFactorEnabledAsync(user, true));
        var recoveryCodes = await this.UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        this.logger.LogInformation("Two-factor enabled for account {UserId}.", user.Id);

        return CallResponseBuilder.CreateResponse<TwoFactorRecoveryCodesModel>(eCallResponseStatus.Success)
            .HasData(new TwoFactorRecoveryCodesModel { RecoveryCodes = recoveryCodes?.ToList() ?? [] });
    }
}
