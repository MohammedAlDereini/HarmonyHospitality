namespace Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>
/// What the browser needs to create a passkey (WebAuthn PublicKeyCredentialCreationOptions as JSON): the challenge, who
/// the key is for, and the rules. Valid for 5 minutes, for one registration, for the caller only.
/// </summary>
public class PasskeyCreationOptionsModel
{
    public string OptionsJson { get; set; } = null!;
}
