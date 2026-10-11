namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

using Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>Step 1 of adding a passkey for the caller: the WebAuthn creation options (the challenge). Nothing is stored yet.</summary>
public class StartPasskeyRegistrationCommand : BaseCommandRequest<CallResponse<PasskeyCreationOptionsModel>>
{
}
