namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoStepExemption;

/// <summary>An admin puts an account on the two-step exception list (id and reason in the body, like Suspend).</summary>
public class ExemptFromTwoStepCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string Reason { get; set; } = null!;

    public class ExemptFromTwoStepCommandValidation : AbstractValidator<ExemptFromTwoStepCommand>
    {
        public ExemptFromTwoStepCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);

            this.RuleFor(e => e.Reason)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.ReasonRequired)
                .MaximumLength(500).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.ReasonTooLong);
        }
    }
}
