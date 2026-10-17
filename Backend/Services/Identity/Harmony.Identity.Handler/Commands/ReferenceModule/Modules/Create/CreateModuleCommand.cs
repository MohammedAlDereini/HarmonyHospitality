namespace Harmony.Identity.Handler.Commands.ReferenceModule.Modules.Create;

using Harmony.Identity.Domain.Entities.Reference;

/// <summary>Adds a module: something a customer subscribes to, per hotel or chain-wide, checked by its code in the owning service.</summary>
public class CreateModuleCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>2 to 64 characters: letters, digits, . - _ like pms.core.</summary>
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    /// <summary>The service that checks subscriptions of this module, like Identity or Fiscal.</summary>
    public string OwnerService { get; set; } = null!;

    /// <summary>Property: sold per hotel (a subscription line names the hotel). Tenant: sold once for the whole customer.</summary>
    public ModuleLevel Level { get; set; }

    /// <summary>True when a subscription line carries a number (rooms, terminals).</summary>
    public bool HasLimit { get; set; }

    public class CreateModuleCommandValidation : AbstractValidator<CreateModuleCommand>
    {
        public CreateModuleCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.ModuleCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.NameEnTooLong);

            this.RuleFor(e => e.OwnerService)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceRequired)
                .MaximumLength(64).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceTooLong);

            this.RuleFor(e => e.Level)
                .IsInEnum().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.LevelInvalid);
        }
    }
}
