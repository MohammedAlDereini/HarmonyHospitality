namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoFactor;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// The caller starts two-factor setup: Identity creates (or reuses) the account's authenticator key and the response carries
/// it once, as the base32 secret and as the otpauth URI for a QR code. The key is stored encrypted (HarmonyUserStore).
/// Nothing is enabled until EnableTwoFactor proves the app has it. Refused while two-factor is already on.
/// </summary>
public class SetupTwoFactorCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<SetupTwoFactorCommand, CallResponse<TwoFactorSetupModel>>
{
    public const string Issuer = "Harmony";

    private readonly ICallContext callContext;

    public SetupTwoFactorCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionRevoker sessionRevoker, ICallContext callContext)
        : base(userManager, cacheService, sessionRevoker)
    {
        this.callContext = callContext;
    }

    public async Task<CallResponse<TwoFactorSetupModel>> Handle(SetupTwoFactorCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.UserId(), out var userId))
        {
            return Fail<TwoFactorSetupModel>(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        var user = await this.LoadAsync(userId);
        if (user is null)
        {
            return Fail<TwoFactorSetupModel>(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (user.TwoFactorEnabled)
        {
            return Fail<TwoFactorSetupModel>(BusinessErrorCodes.Identity.UserAccount.TwoFactorAlreadyEnabled);
        }

        var key = await this.UserManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            EnsureSucceeded(await this.UserManager.ResetAuthenticatorKeyAsync(user));
            key = await this.UserManager.GetAuthenticatorKeyAsync(user);
        }

        var account = user.Email ?? user.UserName!;
        var model = new TwoFactorSetupModel
        {
            SharedKey = key!,
            AuthenticatorUri = $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(account)}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6",
        };

        return CallResponseBuilder.CreateResponse<TwoFactorSetupModel>(eCallResponseStatus.Success).HasData(model);
    }
}
