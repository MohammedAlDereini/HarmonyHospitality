namespace Harmony.Identity.Handler.Commands.RoleModule.Permissions;

public class RevokeRolePermissionCommand : BaseCommandRequest<CallResponse>
{
    public Guid RoleId { get; set; }

    public string PermissionCode { get; set; } = null!;

    public class RevokeRolePermissionCommandValidation : AbstractValidator<RevokeRolePermissionCommand>
    {
        public RevokeRolePermissionCommandValidation()
        {
            this.RuleFor(e => e.RoleId)
                .NotEmpty().WithMessage("This field is required.");

            this.RuleFor(e => e.PermissionCode)
                .NotEmpty().WithMessage("This field is required.")
                .MaximumLength(100).WithMessage("Permission code must not exceed 100 characters.");
        }
    }
}