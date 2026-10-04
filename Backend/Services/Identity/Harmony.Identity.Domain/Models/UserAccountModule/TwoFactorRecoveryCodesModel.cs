namespace Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>The recovery codes, shown exactly once when two-factor turns on. Each works one time, when the app is lost.</summary>
public class TwoFactorRecoveryCodesModel
{
    public IReadOnlyList<string> RecoveryCodes { get; set; } = [];
}
