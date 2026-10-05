namespace Harmony.Identity.Handler.Commands.ReferenceModule.CountrySubdivisions.Edit;

/// <summary>Changes a subdivision's names and kind. Code and country stay.</summary>
public class EditCountrySubdivisionCommand : BaseCommandRequest<CallResponse>
{
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public class EditCountrySubdivisionCommandValidation : AbstractValidator<EditCountrySubdivisionCommand>
    {
        public EditCountrySubdivisionCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.SubdivisionCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.SubdivisionCodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArTooLong);

            this.RuleFor(e => e.Kind)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.SubdivisionKindRequired)
                .MaximumLength(32).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.SubdivisionKindTooLong);
        }
    }
}
