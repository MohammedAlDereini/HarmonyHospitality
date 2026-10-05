namespace Harmony.Identity.Handler.Commands.ReferenceModule.Languages.Edit;

/// <summary>Changes a language's names, direction and active flag. The code stays.</summary>
public class EditLanguageCommand : BaseCommandRequest<CallResponse>
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameNative { get; set; } = null!;

    public bool IsRightToLeft { get; set; }

    public bool IsActive { get; set; } = true;

    public class EditLanguageCommandValidation : AbstractValidator<EditLanguageCommand>
    {
        public EditLanguageCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.LanguageCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.NameNative)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameNativeRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameNativeTooLong);
        }
    }
}
