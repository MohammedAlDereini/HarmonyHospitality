namespace Harmony.Identity.Handler.Commands.ReferenceModule.Cities.Edit;

/// <summary>Changes a city. The country is in the id and stays; everything else can change.</summary>
public class EditCityCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string? SubdivisionCode { get; set; }

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string TimeZoneId { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public bool IsActive { get; set; } = true;

    public class EditCityCommandValidation : AbstractValidator<EditCityCommand>
    {
        public EditCityCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.IdRequired);

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
