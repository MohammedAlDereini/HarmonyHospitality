namespace Harmony.Identity.Handler.Commands.RoleModule.Permissions;

/// <summary>The role holds exactly these permissions afterwards (PropX UpdateRolePermissions).</summary>
public class UpdateRolePermissionsCommand : BaseCommandRequest<CallResponse>
{
    public Guid RoleId { get; set; }

    public List<Guid> PermissionIds { get; set; } = [];

    public class UpdateRolePermissionsCommandValidation : AbstractValidator<UpdateRolePermissionsCommand>
    {
        public UpdateRolePermissionsCommandValidation()
        {
            this.RuleFor(e => e.RoleId)
                .NotEmpty().WithMessage("This field is required.");

            this.RuleFor(e => e.PermissionIds)
                .NotNull().WithMessage("This field is required.");

            this.RuleForEach(e => e.PermissionIds)
                .NotEmpty().WithMessage("Permission id must not be empty.");
        }
    }
}
