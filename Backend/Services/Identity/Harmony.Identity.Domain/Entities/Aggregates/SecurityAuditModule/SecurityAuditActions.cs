namespace Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;

/// <summary>
/// The actions this version writes to the security trail. The column itself is free text: these are names, not a list the
/// database checks, so a row from a newer version is never refused.
/// </summary>
public static class SecurityAuditActions
{
    // The account
    public const string AccountCreated = "account.created";
    public const string AccountSuspended = "account.suspended";
    public const string AccountReinstated = "account.reinstated";
    public const string AccountTerminated = "account.terminated";
    public const string RolesChanged = "account.roles-changed";

    // The password
    public const string PasswordChanged = "password.changed";
    public const string PasswordResetByLink = "password.reset-by-link";

    // Two-step
    public const string TwoStepTurnedOn = "two-step.turned-on";
    public const string TwoStepTurnedOff = "two-step.turned-off";
    public const string TwoStepResetByAdmin = "two-step.reset-by-admin";
    public const string TwoStepExempted = "two-step.exempted";
    public const string TwoStepExemptionEnded = "two-step.exemption-ended";
    public const string BackupCodeUsed = "two-step.backup-code-used";

    // Sessions
    public const string SignedOutEverywhere = "session.signed-out-everywhere";
    public const string SignedOutDevice = "session.signed-out-device";

    // Sign-ins on the sign-in pages
    public const string SignInPasswordOnly = "sign-in.password-only";
    public const string SignInTwoStep = "sign-in.two-step";
    public const string SignInEmailLink = "sign-in.email-link";
    public const string SignInAfterSetup = "sign-in.after-two-step-setup";

    // Sign-ins through the old password grant (until the web app signs in through the BFF)
    public const string SignInPasswordGrant = "sign-in.password-grant";
    public const string SignInPasswordGrantTwoStep = "sign-in.password-grant-two-step";

    // Refused sign-ins
    public const string SignInWrongPassword = "sign-in.wrong-password";
    public const string SignInWrongCode = "sign-in.wrong-code";
    public const string SignInLockedOut = "sign-in.locked-out";
    public const string SignInNotActive = "sign-in.account-not-active";
}
