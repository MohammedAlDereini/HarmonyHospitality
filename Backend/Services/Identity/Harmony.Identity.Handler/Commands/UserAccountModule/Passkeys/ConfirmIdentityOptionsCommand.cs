namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

using Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>Step 1 of proving it is you with a passkey: the WebAuthn request options naming the caller's own keys.</summary>
public class ConfirmIdentityOptionsCommand : BaseCommandRequest<CallResponse<PasskeyRequestOptionsModel>>
{
}
