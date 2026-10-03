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
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Role.IdRequired);

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
        }
    }
}