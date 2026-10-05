namespace Harmony.Identity.Handler.Commands.ReferenceModule.PropertyTypes.Edit;

/// <summary>Changes a property type's names, order and active flag. The code stays.</summary>
public class EditPropertyTypeCommand : BaseCommandRequest<CallResponse>
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public class EditPropertyTypeCommandValidation : AbstractValidator<EditPropertyTypeCommand>
    {
        public EditPropertyTypeCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.CodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArTooLong);

            this.RuleFor(e => e.DisplayOrder)
                .GreaterThanOrEqualTo(0).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.DisplayOrderNegative);
        }
    }
}
