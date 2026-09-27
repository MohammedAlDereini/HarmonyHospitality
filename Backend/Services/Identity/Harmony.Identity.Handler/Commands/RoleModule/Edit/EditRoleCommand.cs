namespace Harmony.Identity.Handler.Commands.RoleModule.Edit;

using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;

public class EditRoleCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string? NameAr { get; set; }

    public PrivilegeLevel PrivilegeLevel { get; set; }

    public class EditRoleCommandValidation : AbstractValidator<EditRoleCommand>
    {
        public EditRoleCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithMessage("This field is required.");

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
        }
    }
}