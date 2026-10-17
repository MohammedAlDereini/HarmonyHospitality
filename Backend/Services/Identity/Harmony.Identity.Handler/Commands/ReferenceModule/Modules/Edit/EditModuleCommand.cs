namespace Harmony.Identity.Handler.Commands.ReferenceModule.Modules.Edit;

using Harmony.Identity.Domain.Entities.Reference;

/// <summary>Changes a module's name, owner, level and limit flag. The code stays: services check it.</summary>
public class EditModuleCommand : BaseCommandRequest<CallResponse>
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string OwnerService { get; set; } = null!;

    public ModuleLevel Level { get; set; }

    public bool HasLimit { get; set; }

    public class EditModuleCommandValidation : AbstractValidator<EditModuleCommand>
    {
        public EditModuleCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.ModuleCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.OwnerService)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceRequired)
                .MaximumLength(64).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceTooLong);

            this.RuleFor(e => e.Level)
                .IsInEnum().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.LevelInvalid);
        }
    }
}
