namespace Harmony.Identity.Handler.Commands.ConfigModule.Settings;

using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>
/// A service's setting is registered (platform only): from then on every customer can set it for the whole chain, a
/// group or one hotel. Adding a setting is a row, not a release (design 5.1). The key and the data type are chosen once.
/// </summary>
public class RegisterConfigSettingCommand : BaseCommandRequest<CallResponse<string>>
{
    /// <summary>Like frontoffice.checkInTime: the owning area first, then dotted words.</summary>
    public string Key { get; set; } = null!;

    /// <summary>The service that reads it and decides what it means, like FrontOffice.</summary>
    public string OwnerService { get; set; } = null!;

    /// <summary>time, int, bool, decimal or string. Fixed once saved.</summary>
    public string DataType { get; set; } = null!;

    /// <summary>What wins when nobody set anything. Optional.</summary>
    public string? SystemDefault { get; set; }

    /// <summary>Optional narrowing to property type codes (HOTEL, RESORT...). Empty: every type.</summary>
    public List<string>? AppliesToTypes { get; set; }

    public class RegisterConfigSettingCommandValidation : AbstractValidator<RegisterConfigSettingCommand>
    {
        public RegisterConfigSettingCommandValidation()
        {
            this.RuleFor(e => e.Key)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Config.KeyRequired)
                .MaximumLength(ConfigSetting.KeyMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Config.KeyInvalidFormat)
                .Matches(ConfigRules.KeyPattern).WithErrorCode(ModelValidationErrorCodes.Identity.Config.KeyInvalidFormat);

            ConfigRules.Shared(this, e => e.OwnerService, e => e.SystemDefault, e => e.AppliesToTypes);

            this.RuleFor(e => e.DataType)
                .Must(type => type is not null && ConfigSetting.DataTypes.Contains(type.Trim().ToLowerInvariant()))
                .WithErrorCode(ModelValidationErrorCodes.Identity.Config.DataTypeInvalid);
        }
    }
}
