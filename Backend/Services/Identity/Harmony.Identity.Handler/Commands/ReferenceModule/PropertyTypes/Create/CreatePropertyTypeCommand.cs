namespace Harmony.Identity.Handler.Commands.ReferenceModule.PropertyTypes.Create;

/// <summary>Adds a property type (Hotel, Hostel, Resort...).</summary>
public class CreatePropertyTypeCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>2 to 32 characters: letters, digits, . - _ (no slash: the code travels in a URL).</summary>
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    /// <summary>Position in lists; zero or more.</summary>
    public int DisplayOrder { get; set; }

    public class CreatePropertyTypeCommandValidation : AbstractValidator<CreatePropertyTypeCommand>
    {
        public CreatePropertyTypeCommandValidation()
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
