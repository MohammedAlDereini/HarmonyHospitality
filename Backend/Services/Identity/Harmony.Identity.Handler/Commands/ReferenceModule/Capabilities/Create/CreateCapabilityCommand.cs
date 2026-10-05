namespace Harmony.Identity.Handler.Commands.ReferenceModule.Capabilities.Create;

/// <summary>Adds a capability: something a tenant can buy, checked by its code in the owning service.</summary>
public class CreateCapabilityCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>2 to 64 characters: letters, digits, . - _ like pms.core.</summary>
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    /// <summary>The service that checks entitlements of this capability, like Identity or Fiscal.</summary>
    public string OwnerService { get; set; } = null!;

    /// <summary>True when an entitlement carries a number (rooms, properties, users).</summary>
    public bool HasLimit { get; set; }

    public class CreateCapabilityCommandValidation : AbstractValidator<CreateCapabilityCommand>
    {
        public CreateCapabilityCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.CapabilityCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.OwnerService)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceRequired)
                .MaximumLength(64).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceTooLong);
        }
    }
}
