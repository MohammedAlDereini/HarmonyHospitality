namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoFactor;

/// <summary>The caller turns two-factor off. The current password is required: a stolen session alone must not be enough.</summary>
public class DisableTwoFactorCommand : BaseCommandRequest<CallResponse>
{
    public string CurrentPassword { get; set; } = null!;

    public class DisableTwoFactorCommandValidation : AbstractValidator<DisableTwoFactorCommand>
    {
        public DisableTwoFactorCommandValidation()
        {
            this.RuleFor(e => e.CurrentPassword)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.CurrentPasswordRequired);
        }
    }
}
