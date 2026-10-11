namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

/// <summary>The caller removes one of their own passkeys. Who: the token's subject; the body carries only the passkey.</summary>
public class RemoveMyPasskeyCommand : BaseCommandRequest<CallResponse>
{
    public Guid PasskeyId { get; set; }

    public class RemoveMyPasskeyCommandValidation : AbstractValidator<RemoveMyPasskeyCommand>
    {
        public RemoveMyPasskeyCommandValidation()
        {
            this.RuleFor(e => e.PasskeyId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.PasskeyIdRequired);
        }
    }
}
