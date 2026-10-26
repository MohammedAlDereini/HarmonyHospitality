namespace Harmony.Identity.Handler.Commands.PropertyGroupModule;

using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;

/// <summary>The brand's own columns, as a door receives them; only for a Brand group (00098 otherwise). Empty values clear.</summary>
public class BrandDetailRequest
{
    public string? ThemeReference { get; set; }

    public Guid? LogoFileId { get; set; }

    public string? WebsiteUrl { get; set; }

    public string? FranchisorName { get; set; }

    public DateOnly? AgreementStartsOn { get; set; }

    public DateOnly? AgreementEndsOn { get; set; }

    public decimal? RoyaltyPercent { get; set; }

    public BrandDetail.Request ToDomain()
        => new(this.ThemeReference, this.LogoFileId, this.WebsiteUrl, this.FranchisorName, this.AgreementStartsOn, this.AgreementEndsOn, this.RoyaltyPercent);

    /// <summary>The rules both doors apply: RuleFor(e => e.Brand!).SetValidator(new BrandDetailRequestValidation()).When(e => e.Brand is not null).</summary>
    public class BrandDetailRequestValidation : AbstractValidator<BrandDetailRequest>
    {
        public BrandDetailRequestValidation()
        {
            this.RuleFor(e => e.ThemeReference)
                .MaximumLength(BrandDetail.ThemeReferenceMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.ThemeReferenceTooLong);

            this.RuleFor(e => e.FranchisorName)
                .MaximumLength(BrandDetail.FranchisorNameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.FranchisorNameTooLong);

            this.RuleFor(e => e.WebsiteUrl)
                .MaximumLength(BrandDetail.WebsiteUrlMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.WebsiteUrlInvalidFormat)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                .WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.WebsiteUrlInvalidFormat)
                .When(e => !string.IsNullOrWhiteSpace(e.WebsiteUrl));

            this.RuleFor(e => e)
                .Must(e => e.AgreementEndsOn is null || e.AgreementStartsOn is null || e.AgreementEndsOn >= e.AgreementStartsOn)
                .WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.AgreementInvalid)
                .WithName(nameof(AgreementEndsOn));

            this.RuleFor(e => e.RoyaltyPercent)
                .InclusiveBetween(0m, BrandDetail.RoyaltyPercentMax).WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.RoyaltyPercentOutOfRange)
                .When(e => e.RoyaltyPercent.HasValue);
        }
    }
}
