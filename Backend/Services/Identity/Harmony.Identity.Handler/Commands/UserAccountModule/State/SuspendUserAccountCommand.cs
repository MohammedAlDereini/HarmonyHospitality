namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

public class SuspendUserAccountCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string Reason { get; set; } = null!;

    public class SuspendUserAccountCommandValidation : AbstractValidator<SuspendUserAccountCommand>
    {
        public SuspendUserAccountCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithMessage("This field is required.");

            this.RuleFor(e => e.Reason)
                .NotEmpty().WithMessage("This field is required.")
                .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
        }
    }
}
