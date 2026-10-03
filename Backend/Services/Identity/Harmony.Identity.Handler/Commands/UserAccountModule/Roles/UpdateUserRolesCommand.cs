namespace Harmony.Identity.Handler.Commands.UserAccountModule.Roles;

public class UpdateUserRolesCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public List<Guid> RoleIds { get; set; } = [];

    public class UpdateUserRolesCommandValidation : AbstractValidator<UpdateUserRolesCommand>
    {
        public UpdateUserRolesCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithMessage("This field is required.");

            this.RuleFor(e => e.RoleIds)
                .NotNull().WithMessage("This field is required.");
        }
    }
}
