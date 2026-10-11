namespace Harmony.Identity.Handler.Commands.UserAccountModule.Passkeys;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>The caller gives one of their passkeys another name. Who: the token's subject; the body carries the passkey and the name.</summary>
public class RenameMyPasskeyCommand : BaseCommandRequest<CallResponse>
{
    public Guid PasskeyId { get; set; }

    public string Name { get; set; } = null!;

    public class RenameMyPasskeyCommandValidation : AbstractValidator<RenameMyPasskeyCommand>
    {
        public RenameMyPasskeyCommandValidation()
        {
            this.RuleFor(e => e.PasskeyId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.PasskeyIdRequired);

            this.RuleFor(e => e.Name)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.PasskeyNameRequired)
                .MaximumLength(UserPasskey.NameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.PasskeyNameTooLong);
        }
    }
}
