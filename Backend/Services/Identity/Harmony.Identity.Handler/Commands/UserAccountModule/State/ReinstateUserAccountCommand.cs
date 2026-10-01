namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

public class ReinstateUserAccountCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public class ReinstateUserAccountCommandValidation : AbstractValidator<ReinstateUserAccountCommand>
    {
        public ReinstateUserAccountCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithMessage("This field is required.");
        }
    }
}
