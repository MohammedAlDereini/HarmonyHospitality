namespace Harmony.Identity.Handler.Commands.UserAccountModule.Password;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>
/// The sign-in walk's own door: a person who proved who they are on the sign-in pages replaces the password somebody
/// else chose, BEFORE being signed in. No controller binds this command — the account id comes from the walk's held
/// proof, never from a request body — and it works only while the account still must change its password.
/// </summary>
public class FirstSignInPasswordChangeCommand : BaseCommandRequest<CallResponse>
{
    public Guid UserId { get; set; }

    public string CurrentPassword { get; set; } = null!;

    public string NewPassword { get; set; } = null!;

    public class FirstSignInPasswordChangeCommandValidation : AbstractValidator<FirstSignInPasswordChangeCommand>
    {
        public FirstSignInPasswordChangeCommandValidation()
        {
            this.RuleFor(e => e.CurrentPassword)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.CurrentPasswordRequired);

            this.RuleFor(e => e.NewPassword)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.NewPasswordRequired)
                .MinimumLength(PasswordRules.MinimumLength).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.NewPasswordTooShort);
        }
    }
}
