namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

/// <summary>An admin removes a passkey from an account (id + passkeyId in the body, like Suspend).</summary>
public class RemovePasskeyCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public Guid PasskeyId { get; set; }

    public class RemovePasskeyCommandValidation : AbstractValidator<RemovePasskeyCommand>
    {
        public RemovePasskeyCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);

            this.RuleFor(e => e.PasskeyId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.PasskeyIdRequired);
        }
    }
}
