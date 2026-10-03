namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

public class TerminateUserAccountCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string Reason { get; set; } = null!;

    public class TerminateUserAccountCommandValidation : AbstractValidator<TerminateUserAccountCommand>
    {
        public TerminateUserAccountCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);

            this.RuleFor(e => e.Reason)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.ReasonRequired)
                .MaximumLength(500).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.ReasonTooLong);
        }
    }
}
