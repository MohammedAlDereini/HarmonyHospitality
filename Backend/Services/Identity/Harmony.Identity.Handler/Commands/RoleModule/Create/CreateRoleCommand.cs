namespace Harmony.Identity.Handler.Commands.RoleModule.Create;

using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

public class CreateRoleCommand : BaseCommandRequest<CallResponse<Guid>>
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string? NameAr { get; set; }

    public PrivilegeLevel PrivilegeLevel { get; set; } = PrivilegeLevel.Standard;

    /// <summary>Ids from GET api/Role/permissions. Empty list = a role with no permissions yet.</summary>
    public List<Guid> PermissionIds { get; set; } = [];

    public class CreateRoleCommandValidation : AbstractValidator<CreateRoleCommand>
    {
        public CreateRoleCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Role.CodeRequired)
                .MaximumLength(32).WithErrorCode(ModelValidationErrorCodes.Identity.Role.CodeTooLong);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Role.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Role.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Role.NameArTooLong)
                .When(e => !string.IsNullOrWhiteSpace(e.NameAr));

            this.RuleFor(e => e.PrivilegeLevel)
                .IsInEnum().WithErrorCode(ModelValidationErrorCodes.Identity.Role.PrivilegeLevelInvalid);

            this.RuleFor(e => e.PermissionIds)
                .NotNull().WithErrorCode(ModelValidationErrorCodes.Identity.Role.PermissionIdsRequired);

            this.RuleForEach(e => e.PermissionIds)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Role.PermissionIdEmpty);
        }
    }
}
