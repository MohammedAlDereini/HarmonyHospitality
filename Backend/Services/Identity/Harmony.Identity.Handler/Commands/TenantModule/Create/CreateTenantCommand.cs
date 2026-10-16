namespace Harmony.Identity.Handler.Commands.TenantModule.Create;

using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;

/// <summary>A new customer. The code is chosen once, here; the status is Active from today.</summary>
public class CreateTenantCommand : BaseCommandRequest<CallResponse<Guid>>
{
    /// <summary>The short name we call the customer by, like RITZ-AMMAN. Stored upper-case.</summary>
    public string Code { get; set; } = null!;

    public string LegalNameEn { get; set; } = null!;

    public string LegalNameAr { get; set; } = null!;

    /// <summary>ISO 3166-1 alpha-2 of a row in Countries.</summary>
    public string CountryCode { get; set; } = null!;

    /// <summary>ISO 4217 of a row in Currencies: the currency of the contract.</summary>
    public string DefaultCurrency { get; set; } = null!;

    /// <summary>1 (smallest) to 5.</summary>
    public int Tier { get; set; } = Tenant.MinTier;

    /// <summary>The contract or billing reference; optional, at most 64 characters.</summary>
    public string? ExternalIdentifier { get; set; }

    public class CreateTenantCommandValidation : AbstractValidator<CreateTenantCommand>
    {
        public CreateTenantCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.CodeRequired)
                .Matches(TenantRules.CodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.CodeInvalidFormat);

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
