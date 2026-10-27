namespace Harmony.Identity.Handler.Commands.ConfigModule.Values;

using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>The rules a place obeys on both value doors.</summary>
public static class ConfigValueRules
{
    public static void Place<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> key,
        Expression<Func<T, string>> scope,
        Expression<Func<T, Guid?>> scopeId)
    {
        validator.RuleFor(key)
            .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Config.KeyRequired)
            .MaximumLength(ConfigSetting.KeyMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Config.KeyInvalidFormat);

        validator.RuleFor(scope)
            .Must(text => LevelOf(text) is not null).WithErrorCode(ModelValidationErrorCodes.Identity.Config.ScopeInvalid);

        var level = scope.Compile();
        validator.RuleFor(scopeId)
            .Must((request, value) => value is { } given && given != Guid.Empty)
            .When(request => LevelOf(level(request)) is ConfigScopeLevel.PropertyGroup or ConfigScopeLevel.Property)
            .WithErrorCode(ModelValidationErrorCodes.Identity.Config.ScopeIdRequired);
    }

    /// <summary>Tenant, PropertyGroup or Property, in any case; null for anything else (numbers included).</summary>
    public static ConfigScopeLevel? LevelOf(string? text)
        => Enum.TryParse<ConfigScopeLevel>(text?.Trim(), ignoreCase: true, out var level)
            && Enum.IsDefined(level)
            && !int.TryParse(text, out _)
            ? level
            : null;
}
