namespace Harmony.Identity.Handler.Commands.ConfigModule.Values;

using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>The value at a place goes: the next lower place wins again.</summary>
public class ClearConfigValueCommand : BaseCommandRequest<CallResponse>
{
    public string Key { get; set; } = null!;

    /// <summary>Tenant, PropertyGroup or Property.</summary>
    public string Scope { get; set; } = null!;

    public Guid? ScopeId { get; set; }

    public class ClearConfigValueCommandValidation : AbstractValidator<ClearConfigValueCommand>
    {
        public ClearConfigValueCommandValidation()
        {
            ConfigValueRules.Place(this, e => e.Key, e => e.Scope, e => e.ScopeId);
        }
    }
}
