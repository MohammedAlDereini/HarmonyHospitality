namespace Harmony.Identity.Handler.Commands.ReferenceModule.Capabilities.Edit;

/// <summary>Changes a capability's name, owner and limit flag. The code stays: services check it.</summary>
public class EditCapabilityCommand : BaseCommandRequest<CallResponse>
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string OwnerService { get; set; } = null!;

    public bool HasLimit { get; set; }

    public class EditCapabilityCommandValidation : AbstractValidator<EditCapabilityCommand>
    {
        public EditCapabilityCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.CapabilityCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.OwnerService)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceRequired)
                .MaximumLength(64).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceTooLong);
        }
    }
}
