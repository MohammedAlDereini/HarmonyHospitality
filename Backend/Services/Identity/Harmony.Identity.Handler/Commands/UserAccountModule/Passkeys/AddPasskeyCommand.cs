namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>Step 2: the browser's answer to the creation options, and what the caller calls the new passkey.</summary>
public class AddPasskeyCommand : BaseCommandRequest<CallResponse<UserPasskeyModel>>
{
    /// <summary>A real answer is a few KB (an attestation object with its certificate chain at most); 64 KB bounds the input.</summary>
    public const int MaxCredentialJsonLength = 65536;

    /// <summary>What the caller calls it; empty means "name it after its maker" (Windows Hello, Apple Passwords...).</summary>
    public string? Name { get; set; }

    /// <summary>The PublicKeyCredential the browser created, as its toJSON() form.</summary>
    public string CredentialJson { get; set; } = null!;

    public class AddPasskeyCommandValidation : AbstractValidator<AddPasskeyCommand>
    {
        public AddPasskeyCommandValidation()
        {
            this.RuleFor(e => e.Name)
                .MaximumLength(UserPasskey.NameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.PasskeyNameTooLong);

            this.RuleFor(e => e.CredentialJson)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.CredentialRequired)
                .MaximumLength(MaxCredentialJsonLength).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.CredentialTooLong);
        }
    }
}
