namespace Harmony.Identity.Handler.Commands.ReferenceModule.Cities.Create;

/// <summary>Adds a city. Its id is derived from country + English name, and the answer carries it.</summary>
public class CreateCityCommand : BaseCommandRequest<CallResponse<Guid>>
{
    /// <summary>ISO 3166-1 alpha-2 of a country in the Countries table.</summary>
    public string CountryCode { get; set; } = null!;

    /// <summary>Optional ISO 3166-2 code of a subdivision of that country, like JO-AM.</summary>
    public string? SubdivisionCode { get; set; }

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    /// <summary>IANA id of a time zone in the TimeZones table, like Asia/Amman.</summary>
    public string TimeZoneId { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public class CreateCityCommandValidation : AbstractValidator<CreateCityCommand>
    {
        public CreateCityCommandValidation()
        {
            this.RuleFor(e => e.CountryCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CountryCodeRequired)
                .Matches(ReferenceRules.CountryCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CountryCodeInvalidFormat);

            this.RuleFor(e => e.SubdivisionCode)
                .Matches(ReferenceRules.SubdivisionCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.SubdivisionCodeInvalidFormat)
                .When(e => !string.IsNullOrWhiteSpace(e.SubdivisionCode));

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArTooLong);

            this.RuleFor(e => e.TimeZoneId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.TimeZoneIdRequired)
                .Matches(ReferenceRules.TimeZoneIdPattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.TimeZoneIdInvalidFormat);

            this.RuleFor(e => e.Latitude)
                .InclusiveBetween(-90m, 90m).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.LatitudeOutOfRange)
                .When(e => e.Latitude.HasValue);

            this.RuleFor(e => e.Longitude)
                .InclusiveBetween(-180m, 180m).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.LongitudeOutOfRange)
                .When(e => e.Longitude.HasValue);
        }
    }
}
