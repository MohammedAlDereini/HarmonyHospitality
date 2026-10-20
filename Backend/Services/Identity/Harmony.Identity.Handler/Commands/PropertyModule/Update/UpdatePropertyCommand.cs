namespace Harmony.Identity.Handler.Commands.PropertyModule.Update;

using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;

/// <summary>
/// Everything the hotel may say about itself, replaced whole: names, address, dates, the rooms it really has, and its
/// own details (an empty value clears the column). Never the code, the place, the money, the language or the regimes.
/// </summary>
public class UpdatePropertyCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string AddressLine { get; set; } = null!;

    public string? PostalCode { get; set; }

    public DateOnly? OpensOn { get; set; }

    public DateOnly? ClosesOn { get; set; }

    /// <summary>The rooms the hotel really has; above the licence it is flagged, never refused.</summary>
    public int TotalRooms { get; set; }

    public string? ShortName { get; set; }

    public string? UniqueName { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? BusinessId { get; set; }

    public string? BusinessRegistrationCode { get; set; }

    public string? BusinessPremiseId { get; set; }

    public int? FiscalYearBeginMonth { get; set; }

    public int? FiscalYearBeginDay { get; set; }

    public string? DefaultCulture { get; set; }

    public int? StarRating { get; set; }

    public string? Email { get; set; }

    public string? Telephone { get; set; }

    public string? WebsiteUrl { get; set; }

    public string? ProductChampionEmail { get; set; }

    public string? LegalOwnerName { get; set; }

    public int? DisplayOrder { get; set; }

    public class UpdatePropertyCommandValidation : AbstractValidator<UpdatePropertyCommand>
    {
        private const string CulturePattern = "^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$";
        private const string TelephonePattern = @"^\+?[0-9 ()./-]{5,32}$";

        public UpdatePropertyCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.IdRequired);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.NameEnRequired)
                .MaximumLength(Property.NameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.NameArRequired)
                .MaximumLength(Property.NameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.NameArTooLong);

            this.RuleFor(e => e.AddressLine)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.AddressRequired)
                .MaximumLength(PostalAddress.LineMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.AddressTooLong);

            this.RuleFor(e => e.PostalCode)
                .MaximumLength(PostalAddress.PostalCodeMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.PostalCodeTooLong);

            this.RuleFor(e => e)
                .Must(e => e.ClosesOn is null || e.OpensOn is null || e.ClosesOn >= e.OpensOn)
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.TermInvalid)
                .WithName(nameof(ClosesOn));

            this.RuleFor(e => e.TotalRooms)
                .GreaterThanOrEqualTo(1).WithErrorCode(ModelValidationErrorCodes.Identity.Property.TotalRoomsOutOfRange);

            this.RuleFor(e => e.ShortName)
                .MaximumLength(Property.ShortNameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.ShortNameTooLong);

            this.RuleFor(e => e.UniqueName)
                .MaximumLength(Property.UniqueNameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.UniqueNameTooLong);

            this.RuleFor(e => e)
                .Must(e => e.Latitude.HasValue == e.Longitude.HasValue && e.Latitude is null or (>= -90 and <= 90) && e.Longitude is null or (>= -180 and <= 180))
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.CoordinatesInvalid)
                .WithName(nameof(Latitude));

            this.RuleFor(e => e.BusinessId)
                .MaximumLength(Property.BusinessTextMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.BusinessIdTooLong);

            this.RuleFor(e => e.BusinessRegistrationCode)
                .MaximumLength(Property.BusinessTextMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.BusinessRegistrationCodeTooLong);

            this.RuleFor(e => e.BusinessPremiseId)
                .MaximumLength(Property.BusinessTextMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.BusinessPremiseIdTooLong);

            this.RuleFor(e => e)
                .Must(e => e.FiscalYearBeginMonth.HasValue == e.FiscalYearBeginDay.HasValue
                    && (e.FiscalYearBeginMonth is null || (e.FiscalYearBeginMonth is >= 1 and <= 12 && e.FiscalYearBeginDay >= 1 && e.FiscalYearBeginDay <= DateTime.DaysInMonth(2025, e.FiscalYearBeginMonth.Value))))
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.FiscalYearBeginInvalid)
                .WithName(nameof(FiscalYearBeginDay));

            this.RuleFor(e => e.DefaultCulture)
                .MaximumLength(Property.DefaultCultureMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.DefaultCultureInvalidFormat)
                .Matches(CulturePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Property.DefaultCultureInvalidFormat)
                .When(e => !string.IsNullOrWhiteSpace(e.DefaultCulture));

            this.RuleFor(e => e.StarRating)
                .InclusiveBetween(1, Property.StarRatingMax).WithErrorCode(ModelValidationErrorCodes.Identity.Property.StarRatingOutOfRange)
                .When(e => e.StarRating.HasValue);

            this.RuleFor(e => e.Email)
                .MaximumLength(Property.EmailMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.EmailInvalidFormat)
                .EmailAddress().WithErrorCode(ModelValidationErrorCodes.Identity.Property.EmailInvalidFormat)
                .When(e => !string.IsNullOrWhiteSpace(e.Email));

            this.RuleFor(e => e.Telephone)
                .Matches(TelephonePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Property.TelephoneInvalidFormat)
                .When(e => !string.IsNullOrWhiteSpace(e.Telephone));

            this.RuleFor(e => e.WebsiteUrl)
                .MaximumLength(Property.WebsiteUrlMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.WebsiteUrlInvalidFormat)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.WebsiteUrlInvalidFormat)
                .When(e => !string.IsNullOrWhiteSpace(e.WebsiteUrl));

            this.RuleFor(e => e.ProductChampionEmail)
                .MaximumLength(Property.EmailMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.ProductChampionEmailInvalidFormat)
                .EmailAddress().WithErrorCode(ModelValidationErrorCodes.Identity.Property.ProductChampionEmailInvalidFormat)
                .When(e => !string.IsNullOrWhiteSpace(e.ProductChampionEmail));

            this.RuleFor(e => e.LegalOwnerName)
                .MaximumLength(Property.LegalOwnerNameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.LegalOwnerNameTooLong);

            this.RuleFor(e => e.DisplayOrder)
                .GreaterThanOrEqualTo(0).WithErrorCode(ModelValidationErrorCodes.Identity.Property.DisplayOrderOutOfRange)
                .When(e => e.DisplayOrder.HasValue);
        }
    }
}
