namespace Harmony.Identity.Handler.Commands.UserAccountModule.Password;

/// <summary>Anyone may ask. The answer is always "ok"; whether a mail went out is not told, so nobody can probe for accounts.</summary>
public class ForgotPasswordCommand : BaseCommandRequest<CallResponse>
{
    public string Email { get; set; } = null!;

    public class ForgotPasswordCommandValidation : AbstractValidator<ForgotPasswordCommand>
    {
        public ForgotPasswordCommandValidation()
        {
            this.RuleFor(e => e.Email)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.EmailRequired)
                .MaximumLength(256).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.EmailTooLong)
                .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.EmailInvalid);
        }
    }
}
