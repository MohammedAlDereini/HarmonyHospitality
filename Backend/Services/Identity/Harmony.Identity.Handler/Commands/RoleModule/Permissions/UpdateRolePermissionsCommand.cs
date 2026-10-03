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
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Role.IdRequired);

            this.RuleFor(e => e.PermissionIds)
                .NotNull().WithErrorCode(ModelValidationErrorCodes.Identity.Role.PermissionIdsRequired);

            this.RuleForEach(e => e.PermissionIds)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Role.PermissionIdEmpty);
        }
    }
}
