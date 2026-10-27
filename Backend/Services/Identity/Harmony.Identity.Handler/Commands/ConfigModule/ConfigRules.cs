namespace Harmony.Identity.Handler.Commands.ConfigModule;

using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>The field rules the settings doors share, written once, mirroring the domain's own checks.</summary>
public static class ConfigRules
{
    /// <summary>The same shape the domain demands: the owning area first, then dotted words, like frontoffice.checkInTime.</summary>
    public const string KeyPattern = "^[a-z][a-z0-9]*(\\.[a-zA-Z][a-zA-Z0-9-]*)+$";

    /// <summary>2 to 64 letters or digits, starting with a letter, like FrontOffice.</summary>
    public const string OwnerPattern = "^[A-Za-z][A-Za-z0-9]{1,63}$";

    /// <summary>The owner, the default and the narrowing: the same rules on Register and Update.</summary>
    public static void Shared<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> owner,
        Expression<Func<T, string?>> systemDefault,
        Expression<Func<T, List<string>?>> appliesToTypes)
    {
        validator.RuleFor(owner)
            .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Config.OwnerServiceRequired)
            .Matches(OwnerPattern).WithErrorCode(ModelValidationErrorCodes.Identity.Config.OwnerServiceInvalidFormat);

        validator.RuleFor(systemDefault)
            .MaximumLength(ConfigSetting.DefaultMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Config.SystemDefaultTooLong);

        validator.RuleFor(appliesToTypes)
            .Must(codes => codes is null || (codes.Count <= ConfigSetting.AppliesToTypesMaxCount
                && codes.All(code => code is not null && code.Trim().Length is >= 2 and <= ConfigSetting.TypeCodeMaxLength && !code.Contains(','))))
            .WithErrorCode(ModelValidationErrorCodes.Identity.Config.AppliesToTypesInvalid);
    }
}
