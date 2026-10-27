namespace Harmony.Identity.Handler.Commands.ConfigModule.Values;

using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>
/// A customer sets a registered key at a place of its own: the whole customer (Tenant), one group (PropertyGroup) or
/// one hotel (Property). The value replaces what was there. Identity stores the string; the owning service decides what
/// it means. 201 with the row's id when the place had nothing, 200 when a value changed.
/// </summary>
public class SetConfigValueCommand : BaseCommandRequest<CallResponse<Guid>>
{
    public string Key { get; set; } = null!;

    /// <summary>Tenant, PropertyGroup or Property.</summary>
    public string Scope { get; set; } = null!;

    /// <summary>The group's or the hotel's id; ignored for the whole customer.</summary>
    public Guid? ScopeId { get; set; }

    public string Value { get; set; } = null!;

    public class SetConfigValueCommandValidation : AbstractValidator<SetConfigValueCommand>
    {
        public SetConfigValueCommandValidation()
        {
            ConfigValueRules.Place(this, e => e.Key, e => e.Scope, e => e.ScopeId);

            this.RuleFor(e => e.Value)
                .Must(value => !string.IsNullOrWhiteSpace(value)).WithErrorCode(ModelValidationErrorCodes.Identity.Config.ValueRequired)
                .Must(value => value is null || value.Trim().Length <= ConfigValue.ValueMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Config.ValueTooLong);
        }
    }
}
