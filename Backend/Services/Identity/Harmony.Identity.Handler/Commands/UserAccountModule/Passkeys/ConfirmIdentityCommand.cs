namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

/// <summary>
/// The caller proves it is them before changing their passkeys: with the password, or with the answer of one of their
/// passkeys to the options of <see cref="ConfirmIdentityOptionsCommand"/>. Exactly one of the two.
/// </summary>
public class ConfirmIdentityCommand : BaseCommandRequest<CallResponse>
{
    public string? Password { get; set; }

    /// <summary>The PublicKeyCredential the browser produced for the confirmation options, as its JSON form.</summary>
    public string? CredentialJson { get; set; }

    public class ConfirmIdentityCommandValidation : AbstractValidator<ConfirmIdentityCommand>
    {
        public ConfirmIdentityCommandValidation()
        {
            this.RuleFor(e => e)
                .Must(e => string.IsNullOrEmpty(e.Password) != string.IsNullOrEmpty(e.CredentialJson))
                .WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.OneProofRequired);

            this.RuleFor(e => e.CredentialJson)
                .MaximumLength(AddPasskeyCommand.MaxCredentialJsonLength).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.CredentialTooLong);
        }
    }
}
