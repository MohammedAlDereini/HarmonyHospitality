namespace Harmony.Identity.Handler.Commands.RoleModule.Delete;

public class DeleteRoleCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public class DeleteRoleCommandValidation : AbstractValidator<DeleteRoleCommand>
    {
        public DeleteRoleCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Role.IdRequired);
        }
    }
}