namespace Harmony.Identity.Domain.Services;

/// <summary>
/// Account events the person is told about by e-mail, so a change they did not make is noticed. NIST SP 800-63B-4 requires a
/// notice when an authenticator is added and on every account recovery (a backup code, a password reset), and recommends one
/// when an authenticator is removed.
/// </summary>
public enum SecurityAlert
{
    TwoStepTurnedOn,
    TwoStepTurnedOff,
    TwoStepResetByAdmin,
    PasswordChanged,
    PasswordSetByLink,
    BackupCodeUsed,
}
