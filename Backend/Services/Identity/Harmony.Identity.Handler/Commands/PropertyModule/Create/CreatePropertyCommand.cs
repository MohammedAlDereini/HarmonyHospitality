namespace Harmony.Identity.Handler.Commands.PropertyModule.Create;

using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

/// <summary>
/// A hotel is born for a customer, with its first subscription lines (platform only). The hotel's identity and licence
/// facts come from the contract; its own details (contacts, fiscal registration, star rating) come later from its
/// people. Country, subdivision and time zone are never typed: the city gives them.
/// </summary>
public class CreatePropertyCommand : BaseCommandRequest<CallResponse<Guid>>
{
    public Guid TenantId { get; set; }

    /// <summary>2 to 32 characters: letters, digits, . - _ like AMMAN. Upper-cased, unique in the customer.</summary>
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string PropertyTypeCode { get; set; } = null!;

    public Guid CityId { get; set; }

    public string AddressLine { get; set; } = null!;

    public string? PostalCode { get; set; }

    public string DefaultCurrency { get; set; } = null!;

    public string DefaultLanguageCode { get; set; } = null!;

    /// <summary>The rooms the hotel really has. Compared with the licensed rooms; a gap is flagged, never refused.</summary>
    public int TotalRooms { get; set; }

    public string LegalEnvironmentCode { get; set; } = null!;

    public string TaxEnvironmentCode { get; set; } = null!;

    public string AccountingEnvironmentCode { get; set; } = null!;

    public string AccommodationEnvironmentCode { get; set; } = null!;

    /// <summary>Accepts reservations from this day; empty = open now.</summary>
    public DateOnly? OpensOn { get; set; }

    public DateOnly? ClosesOn { get; set; }

    public string? ExternalIdentifier { get; set; }

    /// <summary>The term of the first lines; EndsOn empty = open-ended.</summary>
    public DateOnly StartsOn { get; set; }

    public DateOnly? EndsOn { get; set; }

    /// <summary>At least one: nobody runs an unlicensed hotel.</summary>
    public List<SubscriptionLineRequest> Lines { get; set; } = [];

    public class SubscriptionLineRequest
    {
        public string ModuleCode { get; set; } = null!;

        public int? Rooms { get; set; }

        public int? Terminals { get; set; }

        /// <summary>True unless the customer already gave notice for this line.</summary>
        public bool RenewsAutomatically { get; set; } = true;
    }

    public class CreatePropertyCommandValidation : AbstractValidator<CreatePropertyCommand>
    {
        public CreatePropertyCommandValidation()
        {
            this.RuleFor(e => e.TenantId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);

            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.CodeRequired)
                .Matches("^[A-Za-z0-9._-]{2,32}$").WithErrorCode(ModelValidationErrorCodes.Identity.Property.CodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.NameEnRequired)
                .MaximumLength(Property.NameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.NameArRequired)
                .MaximumLength(Property.NameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.NameArTooLong);

            this.RuleFor(e => e.PropertyTypeCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.PropertyTypeRequired);

            this.RuleFor(e => e.CityId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.CityRequired);

            this.RuleFor(e => e.AddressLine)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.AddressRequired)
                .MaximumLength(PostalAddress.LineMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.AddressTooLong);

            this.RuleFor(e => e.PostalCode)
                .MaximumLength(PostalAddress.PostalCodeMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.PostalCodeTooLong);

            this.RuleFor(e => e.DefaultCurrency)
                .Matches("^[A-Za-z]{3}$").WithErrorCode(ModelValidationErrorCodes.Identity.Property.CurrencyInvalidFormat);

            this.RuleFor(e => e.DefaultLanguageCode)
                .Matches("^[A-Za-z]{2}$").WithErrorCode(ModelValidationErrorCodes.Identity.Property.LanguageInvalidFormat);

            this.RuleFor(e => e.TotalRooms)
                .GreaterThanOrEqualTo(1).WithErrorCode(ModelValidationErrorCodes.Identity.Property.TotalRoomsOutOfRange);

            this.RuleFor(e => e.LegalEnvironmentCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.EnvironmentRequired);
            this.RuleFor(e => e.TaxEnvironmentCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.EnvironmentRequired);
            this.RuleFor(e => e.AccountingEnvironmentCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.EnvironmentRequired);
            this.RuleFor(e => e.AccommodationEnvironmentCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.EnvironmentRequired);

            this.RuleFor(e => e.ExternalIdentifier)
                .MaximumLength(Property.ExternalIdentifierMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.ExternalIdentifierTooLong);

            this.RuleFor(e => e)
                .Must(e => e.ClosesOn is null || e.OpensOn is null || e.ClosesOn >= e.OpensOn)
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.TermInvalid)
                .WithName(nameof(ClosesOn));

            // The term: a real start, an end not before it, and an end whenever any line gives notice.
            this.RuleFor(e => e.StartsOn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.TermInvalid);

            this.RuleFor(e => e)
                .Must(e => e.EndsOn is null || e.EndsOn >= e.StartsOn)
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.TermInvalid)
                .WithName(nameof(EndsOn));

            this.RuleFor(e => e)
                .Must(e => e.EndsOn is not null || e.Lines.All(l => l.RenewsAutomatically))
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.TermInvalid)
                .WithName(nameof(EndsOn));

            this.RuleFor(e => e.Lines)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.LinesRequired);

            this.RuleFor(e => e.Lines)
                .Must(lines => lines.Select(l => l.ModuleCode?.Trim().ToUpperInvariant()).Distinct().Count() == lines.Count)
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.LineModuleDuplicate)
                .When(e => e.Lines.Count > 0);

            this.RuleForEach(e => e.Lines).ChildRules(line =>
            {
                line.RuleFor(l => l.ModuleCode)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                    .Matches(ReferenceRules.ModuleCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

                line.RuleFor(l => l.Rooms)
                    .GreaterThanOrEqualTo(1).WithErrorCode(ModelValidationErrorCodes.Identity.Property.LineRoomsOutOfRange)
                    .When(l => l.Rooms.HasValue);

                line.RuleFor(l => l.Terminals)
                    .GreaterThanOrEqualTo(1).WithErrorCode(ModelValidationErrorCodes.Identity.Property.LineTerminalsOutOfRange)
                    .When(l => l.Terminals.HasValue);
            });
        }
    }
}
