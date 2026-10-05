namespace Harmony.Identity.Handler.Commands.ReferenceModule.PropertyGroupTypes.Create;

/// <summary>Adds a property group type (Brand, Region, Owner, Custom...).</summary>
public class CreatePropertyGroupTypeCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>2 to 32 characters: letters, digits, . - _ (no slash: the code travels in a URL).</summary>
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    /// <summary>A property may belong to one group of an exclusive type (one brand per hotel).</summary>
    public bool IsExclusive { get; set; }

    /// <summary>
    /// True when code relies on this row (Brand is read by the one-brand-per-hotel rule): it can then be renamed but its
    /// exclusivity is locked. Set once, here, by the platform admin who installs the row.
    /// </summary>
    public bool IsSystem { get; set; }

    public class CreatePropertyGroupTypeCommandValidation : AbstractValidator<CreatePropertyGroupTypeCommand>
    {
        public CreatePropertyGroupTypeCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.CodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArTooLong);
        }
    }
}
