namespace Harmony.Identity.Handler.Commands.ConfigModule.Settings;

using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>What the platform may change about a registered setting, replaced whole. Never the key or the data type.</summary>
public class UpdateConfigSettingCommand : BaseCommandRequest<CallResponse>
{
    public string Key { get; set; } = null!;

    public string OwnerService { get; set; } = null!;

    public string? SystemDefault { get; set; }

    public List<string>? AppliesToTypes { get; set; }

    /// <summary>Switched off: no value is set for it and the readers stop seeing it. The values stay.</summary>
    public bool IsActive { get; set; }

    public class UpdateConfigSettingCommandValidation : AbstractValidator<UpdateConfigSettingCommand>
    {
        public UpdateConfigSettingCommandValidation()
        {
            this.RuleFor(e => e.Key)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Config.KeyRequired)
                .MaximumLength(ConfigSetting.KeyMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Config.KeyInvalidFormat);

            ConfigRules.Shared(this, e => e.OwnerService, e => e.SystemDefault, e => e.AppliesToTypes);
        }
    }
}
