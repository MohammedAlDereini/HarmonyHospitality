namespace Harmony.Identity.Handler.Commands.ReferenceModule.Countries.Create;

/// <summary>Adds a country (ISO 3166-1). Code and alpha-3 are its identity and are chosen once, here.</summary>
public class CreateCountryCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>ISO 3166-1 alpha-2, like JO. Stored upper-case.</summary>
    public string Code { get; set; } = null!;

    /// <summary>ISO 3166-1 alpha-3, like JOR. Stored upper-case.</summary>
    public string Alpha3 { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    /// <summary>Like +962.</summary>
    public string DialCode { get; set; } = null!;

    /// <summary>ISO 4217 code of a currency in the Currencies table.</summary>
    public string DefaultCurrency { get; set; } = null!;

    public class CreateCountryCommandValidation : AbstractValidator<CreateCountryCommand>
    {
        public CreateCountryCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.CountryCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.Alpha3)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.Alpha3Required)
                .Matches(ReferenceRules.Alpha3Pattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.Alpha3InvalidFormat);

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
