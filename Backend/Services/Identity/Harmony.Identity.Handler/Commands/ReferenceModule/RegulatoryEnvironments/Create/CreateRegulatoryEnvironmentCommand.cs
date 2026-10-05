namespace Harmony.Identity.Handler.Commands.ReferenceModule.RegulatoryEnvironments.Create;

using Harmony.Identity.Domain.Entities.Reference;

/// <summary>Adds a regulatory environment. The code is the key other tables will point at, so it is chosen once, here.</summary>
public class CreateRegulatoryEnvironmentCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>2 to 32 characters: letters, digits, . - _ (no slash: the code travels in a URL). Example: JO-GST.</summary>
    public string Code { get; set; } = null!;

    public RegulatoryKind Kind { get; set; }

    /// <summary>ISO 3166-1 alpha-2 of a country in the Countries table.</summary>
    public string CountryCode { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    /// <summary>The service whose rules the row stands for, like Fiscal.</summary>
    public string OwnerService { get; set; } = null!;

    public class CreateRegulatoryEnvironmentCommandValidation : AbstractValidator<CreateRegulatoryEnvironmentCommand>
    {
        public CreateRegulatoryEnvironmentCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.CodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.Kind)
                .IsInEnum().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.KindInvalid);

            this.RuleFor(e => e.CountryCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CountryCodeRequired)
                .Matches(ReferenceRules.CountryCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CountryCodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArTooLong);

            this.RuleFor(e => e.OwnerService)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceRequired)
                .MaximumLength(64).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceTooLong);
        }
    }
}
