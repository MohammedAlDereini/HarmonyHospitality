namespace Harmony.Identity.Handler.Commands.UserAccountModule.TwoStepExemption;

/// <summary>An admin takes an account off the two-step exception list (id in the body, like Reinstate).</summary>
public class EndTwoStepExemptionCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public class EndTwoStepExemptionCommandValidation : AbstractValidator<EndTwoStepExemptionCommand>
    {
        public EndTwoStepExemptionCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
        }
    }
}
