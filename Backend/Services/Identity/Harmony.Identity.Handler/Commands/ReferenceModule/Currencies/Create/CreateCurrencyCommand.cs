namespace Harmony.Identity.Handler.Commands.ReferenceModule.Currencies.Create;

/// <summary>Adds a currency (ISO 4217). Minor units are not typed: they come from the framework Money registry.</summary>
public class CreateCurrencyCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>ISO 4217, like JOD. Stored upper-case; must be one the framework Money registry knows.</summary>
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public class CreateCurrencyCommandValidation : AbstractValidator<CreateCurrencyCommand>
    {
        public CreateCurrencyCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.CurrencyCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArTooLong);
        }
    }
}
