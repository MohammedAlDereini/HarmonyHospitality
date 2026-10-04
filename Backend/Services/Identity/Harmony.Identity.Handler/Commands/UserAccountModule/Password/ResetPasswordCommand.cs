namespace Harmony.Identity.Handler.Commands.UserAccountModule.Password;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>A new password with the one-time token from a reset or invitation link. Works for an account with no password yet.</summary>
public class ResetPasswordCommand : BaseCommandRequest<CallResponse>
{
    public string Email { get; set; } = null!;

    public string Token { get; set; } = null!;

    public string NewPassword { get; set; } = null!;

    public class ResetPasswordCommandValidation : AbstractValidator<ResetPasswordCommand>
    {
        public ResetPasswordCommandValidation()
        {
            this.RuleFor(e => e.Email)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.EmailRequired)
                .MaximumLength(256).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.EmailTooLong)
                .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.EmailInvalid);

            this.RuleFor(e => e.Token)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.TokenRequired);

            this.RuleFor(e => e.NewPassword)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.NewPasswordRequired)
                .MinimumLength(PasswordRules.MinimumLength).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.NewPasswordTooShort);
        }
    }
}
