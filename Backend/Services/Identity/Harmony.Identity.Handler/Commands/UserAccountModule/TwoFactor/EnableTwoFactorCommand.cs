namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoFactor;

using Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>Step 2: the 6-digit code from the app proves the setup; two-factor turns on and the recovery codes come back once.</summary>
public class EnableTwoFactorCommand : BaseCommandRequest<CallResponse<TwoFactorRecoveryCodesModel>>
{
    public string Code { get; set; } = null!;

    public class EnableTwoFactorCommandValidation : AbstractValidator<EnableTwoFactorCommand>
    {
        public EnableTwoFactorCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.CodeRequired)
                .Matches("^[0-9]{6}$").WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.CodeInvalidFormat);
        }
    }
}
