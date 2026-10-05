namespace Harmony.Identity.Handler.Commands.ReferenceModule.TimeZones.Edit;

/// <summary>Changes a time zone's names and active flag. The id stays.</summary>
public class EditTimeZoneCommand : BaseCommandRequest<CallResponse>
{
    public string Id { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public class EditTimeZoneCommandValidation : AbstractValidator<EditTimeZoneCommand>
    {
        public EditTimeZoneCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.TimeZoneIdRequired)
                .Matches(ReferenceRules.TimeZoneIdPattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.TimeZoneIdInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameArTooLong);
        }
    }
}
