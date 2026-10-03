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
                .NotEmpty().WithMessage("This field is required.")
                .MaximumLength(32).WithMessage("Role code must not exceed 32 characters.");

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithMessage("This field is required.")
                .MaximumLength(128).WithMessage("Role name must not exceed 128 characters.");

            this.RuleFor(e => e.NameAr)
                .MaximumLength(128).WithMessage("Arabic name must not exceed 128 characters.")
                .When(e => !string.IsNullOrWhiteSpace(e.NameAr));

            this.RuleFor(e => e.PrivilegeLevel)
                .IsInEnum().WithMessage("Privilege level is not valid.");

            this.RuleFor(e => e.PermissionIds)
                .NotNull().WithMessage("Permission ids must be a list.");

            this.RuleForEach(e => e.PermissionIds)
                .NotEmpty().WithMessage("Permission id must not be empty.");
        }
    }
}
