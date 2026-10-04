namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoFactor;

/// <summary>An admin removes a lost authenticator from an account (id in the body, like Suspend). Every session of that account ends.</summary>
public class ResetTwoFactorCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public class ResetTwoFactorCommandValidation : AbstractValidator<ResetTwoFactorCommand>
    {
        public ResetTwoFactorCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
        }
    }
}
