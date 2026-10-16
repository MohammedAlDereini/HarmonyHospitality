namespace Harmony.Identity.Handler.Commands.TenantModule.Edit;

using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;

/// <summary>Everything about a customer but its code and its status. The platform's own row may be completed here too.</summary>
public class EditTenantCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string LegalNameEn { get; set; } = null!;

    public string LegalNameAr { get; set; } = null!;

    public string CountryCode { get; set; } = null!;

    public string DefaultCurrency { get; set; } = null!;

    public int Tier { get; set; } = Tenant.MinTier;

    public string? ExternalIdentifier { get; set; }

    public class EditTenantCommandValidation : AbstractValidator<EditTenantCommand>
    {
        public EditTenantCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);

            this.RuleFor(e => e.LegalNameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.LegalNameEnRequired)
                .MaximumLength(Tenant.LegalNameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.LegalNameEnTooLong);

            this.RuleFor(e => e.LegalNameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.LegalNameArRequired)
                .MaximumLength(Tenant.LegalNameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.LegalNameArTooLong);

            this.RuleFor(e => e.CountryCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.CountryCodeRequired)
                .Matches(TenantRules.CountryCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.CountryCodeInvalidFormat);

            this.RuleFor(e => e.DefaultCurrency)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.DefaultCurrencyRequired)
                .Matches(TenantRules.CurrencyCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.DefaultCurrencyInvalidFormat);

            this.RuleFor(e => e.Tier)
                .InclusiveBetween(Tenant.MinTier, Tenant.MaxTier).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.TierOutOfRange);

            this.RuleFor(e => e.ExternalIdentifier)
                .MaximumLength(Tenant.ExternalIdentifierMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.ExternalIdentifierTooLong);
        }
    }
}
