namespace Harmony.Identity.Handler.Commands.ReferenceModule.TimeZones.Create;

/// <summary>Adds a time zone (IANA id). The runtime must be able to resolve it, or no property could compute a business date.</summary>
public class CreateTimeZoneCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>IANA id, like Asia/Amman.</summary>
    public string Id { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public class CreateTimeZoneCommandValidation : AbstractValidator<CreateTimeZoneCommand>
    {
        public CreateTimeZoneCommandValidation()
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
