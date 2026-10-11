namespace Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>
/// What the browser needs to answer with one of the caller's own passkeys (WebAuthn PublicKeyCredentialRequestOptions as
/// JSON), to confirm it is them before a passkey is added or removed. Valid for 5 minutes, for one answer, for the caller only.
/// </summary>
public class PasskeyRequestOptionsModel
{
    public string OptionsJson { get; set; } = null!;
}
