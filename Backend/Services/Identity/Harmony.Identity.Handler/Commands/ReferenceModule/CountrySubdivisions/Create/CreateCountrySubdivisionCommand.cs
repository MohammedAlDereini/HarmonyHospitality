namespace Harmony.Identity.Handler.Commands.ReferenceModule.CountrySubdivisions.Create;

/// <summary>Adds a subdivision (ISO 3166-2) of a country in the Countries table.</summary>
public class CreateCountrySubdivisionCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>ISO 3166-1 alpha-2 of the country, like JO.</summary>
    public string CountryCode { get; set; } = null!;

    /// <summary>ISO 3166-2, carrying the country: JO-AM. Stored upper-case.</summary>
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    /// <summary>Governorate, Region, Emirate, State...</summary>
    public string Kind { get; set; } = null!;

    public class CreateCountrySubdivisionCommandValidation : AbstractValidator<CreateCountrySubdivisionCommand>
    {
        public CreateCountrySubdivisionCommandValidation()
        {
            this.RuleFor(e => e.CountryCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CountryCodeRequired)
                .Matches(ReferenceRules.CountryCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CountryCodeInvalidFormat);

            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.SubdivisionCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.SubdivisionCodeInvalidFormat);

            // The code must carry the country it is created under (JO-AM under JO), or the row contradicts itself.
            this.RuleFor(e => e)
                .Must(e => e.Code.Trim().StartsWith(e.CountryCode.Trim() + "-", StringComparison.OrdinalIgnoreCase))
                .WithErrorCode(ModelValidationErrorCodes.Identity.Reference.SubdivisionCodeInvalidFormat)
                .When(e => !string.IsNullOrWhiteSpace(e.Code) && !string.IsNullOrWhiteSpace(e.CountryCode));

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArTooLong);

            this.RuleFor(e => e.Kind)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.SubdivisionKindRequired)
                .MaximumLength(32).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.SubdivisionKindTooLong);
        }
    }
}
