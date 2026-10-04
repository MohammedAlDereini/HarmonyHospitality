namespace Harmony.Identity.Handler.Commands.UserAccountModule.Password;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>The caller replaces their own password. Who: the token's subject, never a body field.</summary>
public class ChangePasswordCommand : BaseCommandRequest<CallResponse>
{
    public string CurrentPassword { get; set; } = null!;

    public string NewPassword { get; set; } = null!;

    public class ChangePasswordCommandValidation : AbstractValidator<ChangePasswordCommand>
    {
        public ChangePasswordCommandValidation()
        {
            this.RuleFor(e => e.CurrentPassword)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.CurrentPasswordRequired);

            this.RuleFor(e => e.NewPassword)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.NewPasswordRequired)
                .MinimumLength(PasswordRules.MinimumLength).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.NewPasswordTooShort);
        }
    }
}
