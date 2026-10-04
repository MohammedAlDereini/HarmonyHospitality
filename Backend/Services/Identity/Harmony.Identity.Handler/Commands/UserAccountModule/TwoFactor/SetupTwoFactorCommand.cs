namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoFactor;

using Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>Step 1 of turning on an authenticator app for the caller: the shared key and QR URI. Nothing is enabled yet.</summary>
public class SetupTwoFactorCommand : BaseCommandRequest<CallResponse<TwoFactorSetupModel>>
{
}
