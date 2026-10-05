namespace Harmony.Identity.Handler.Commands.ReferenceModule.Countries.Edit;

/// <summary>Changes a country's names, dial code, default currency and active flag. Code and alpha-3 stay.</summary>
public class EditCountryCommand : BaseCommandRequest<CallResponse>
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string DialCode { get; set; } = null!;

    public string DefaultCurrency { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public class EditCountryCommandValidation : AbstractValidator<EditCountryCommand>
    {
        public EditCountryCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.CountryCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArTooLong);

            this.RuleFor(e => e.DialCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.DialCodeRequired)
                .Matches(ReferenceRules.DialCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.DialCodeInvalidFormat);

            this.RuleFor(e => e.DefaultCurrency)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.DefaultCurrencyRequired)
                .Matches(ReferenceRules.CurrencyCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.DefaultCurrencyInvalidFormat);
        }
    }
}
