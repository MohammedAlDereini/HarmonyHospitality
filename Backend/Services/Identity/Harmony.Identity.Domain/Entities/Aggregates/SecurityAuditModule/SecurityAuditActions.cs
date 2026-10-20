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

    /// <summary>The person proved it was them again before changing their passkeys (the detail says how: password or passkey).</summary>
    public const string IdentityConfirmed = "identity.confirmed";

    // Passkeys
    public const string PasskeyAdded = "passkey.added";
    public const string PasskeyRenamed = "passkey.renamed";
    public const string PasskeyRemoved = "passkey.removed";
    public const string PasskeyRemovedByAdmin = "passkey.removed-by-admin";

    // Sessions
    public const string SignedOutEverywhere = "session.signed-out-everywhere";
    public const string SignedOutDevice = "session.signed-out-device";

    // Sign-ins on the sign-in pages
    public const string SignInPasswordOnly = "sign-in.password-only";
    public const string SignInTwoStep = "sign-in.two-step";
    public const string SignInEmailLink = "sign-in.email-link";
    public const string SignInAfterSetup = "sign-in.after-two-step-setup";
    public const string SignInPasskey = "sign-in.passkey";

    // Rows written before BFF step 5 may still carry "sign-in.password-grant" and "sign-in.password-grant-two-step":
    // the old web app's grant, gone with step 5. The trail keeps them as history; nothing writes them any more.

    // Refused sign-ins
    public const string SignInWrongPassword = "sign-in.wrong-password";
    public const string SignInWrongCode = "sign-in.wrong-code";
    public const string SignInLockedOut = "sign-in.locked-out";
    public const string SignInNotActive = "sign-in.account-not-active";

    /// <summary>The account is fine, its company is not: the tenant is suspended or terminated.</summary>
    public const string SignInTenantNotActive = "sign-in.tenant-not-active";

    // The platform console's work on customers, written to the ACTING platform person's trail; Detail = the tenant code.
    public const string TenantCreated = "tenant.created";
    public const string TenantUpdated = "tenant.updated";
    public const string TenantSuspended = "tenant.suspended";
    public const string TenantReinstated = "tenant.reinstated";

    /// <summary>The platform invited (or re-invited) a tenant's administrator; the detail is the tenant code.</summary>
    public const string TenantAdministratorInvited = "tenant.administrator-invited";

    /// <summary>The administrator account exists but the invitation mail could not be sent; InviteAdministrator repairs it.</summary>
    public const string TenantAdministratorInvitationFailed = "tenant.administrator-invitation-failed";

    /// <summary>The platform created a hotel for a customer; the detail is the hotel code.</summary>
    public const string PropertyCreated = "property.created";

    /// <summary>Subscription lines were written for a customer; the detail is "HOTEL:module,module" (chain-wide: "-:module").</summary>
    public const string TenantSubscribed = "tenant.subscribed";

    /// <summary>A subscription line was switched off; the detail is "HOTEL:module:reason" (chain-wide: "-:module:reason").</summary>
    public const string TenantUnsubscribed = "tenant.unsubscribed";

    /// <summary>The hotel's rooms exceed the rooms licensed on its lines; the detail is "HOTEL:rooms>licensed". Flagged for finance, never refused.</summary>
    public const string PropertyRoomsAboveLicence = "property.rooms-above-licence";

    /// <summary>A hotel's own details were changed by its people; the detail is the hotel code.</summary>
    public const string PropertyUpdated = "property.updated";

    /// <summary>A passkey answer of a known account that did not verify, or a passkey removed meanwhile.</summary>
    public const string SignInPasskeyRefused = "sign-in.passkey-refused";
}
