namespace Harmony.Identity.Handler.Commands.UserAccountModule.Access;

/// <summary>Ends an assignment now. The row stays, with its end.</summary>
public class EndAssignmentCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public Guid AssignmentId { get; set; }

    public class EndAssignmentCommandValidation : AbstractValidator<EndAssignmentCommand>
    {
        public EndAssignmentCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
            this.RuleFor(e => e.AssignmentId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.AssignmentIdRequired);
        }
    }
}
