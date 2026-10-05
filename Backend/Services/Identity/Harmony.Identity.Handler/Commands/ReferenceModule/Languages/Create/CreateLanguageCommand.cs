namespace Harmony.Identity.Handler.Commands.ReferenceModule.Languages.Create;

/// <summary>Adds a language (ISO 639-1).</summary>
public class CreateLanguageCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>ISO 639-1, like ar. Stored lower-case.</summary>
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    /// <summary>The name in the language itself, like العربية.</summary>
    public string NameNative { get; set; } = null!;

    public bool IsRightToLeft { get; set; }

    public class CreateLanguageCommandValidation : AbstractValidator<CreateLanguageCommand>
    {
        public CreateLanguageCommandValidation()
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
