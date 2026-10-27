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

    /// <summary>A hotel's contact list was replaced by its people; the detail is "HOTEL:count".</summary>
    public const string PropertyContactsChanged = "property.contacts-changed";

    /// <summary>The platform made a hotel group (brand, region, owner) for a customer; the detail is the group code.</summary>
    public const string PropertyGroupCreated = "property-group.created";

    /// <summary>The platform changed a hotel group; the detail is the group code.</summary>
    public const string PropertyGroupUpdated = "property-group.updated";

    /// <summary>A hotel joined a group; the detail is "GROUP:HOTEL".</summary>
    public const string PropertyGroupMemberAdded = "property-group.member-added";

    /// <summary>A hotel's membership was closed; the detail is "GROUP:HOTEL".</summary>
    public const string PropertyGroupMemberEnded = "property-group.member-ended";

    /// <summary>A role was given at a hotel or a group; the detail is "ROLE@HOTEL" or "ROLE@GROUP".</summary>
    public const string RoleAssigned = "account.role-assigned";
    public const string RoleAssignmentEnded = "account.role-assignment-ended";

    /// <summary>A temporary elevation; the detail is "ROLE@SCOPE until <ToUtc>".</summary>
    public const string AccessGranted = "account.access-granted";
    public const string AccessGrantRevoked = "account.access-grant-revoked";

    /// <summary>The platform registered a setting key; the detail is the key.</summary>
    public const string ConfigSettingRegistered = "config.setting-registered";
    public const string ConfigSettingUpdated = "config.setting-updated";

    /// <summary>A customer set or cleared a value; the detail is "KEY@PLACE" (the hotel's or group's code, or the customer's).</summary>
    public const string ConfigValueSet = "config.value-set";
    public const string ConfigValueCleared = "config.value-cleared";

    /// <summary>A call named a hotel the person may not act at; the detail is the hotel id. Written on the person's own trail.</summary>
    public const string PropertyAccessRefused = "access.property-refused";

    /// <summary>A passkey answer of a known account that did not verify, or a passkey removed meanwhile.</summary>
    public const string SignInPasskeyRefused = "sign-in.passkey-refused";

    /// <summary>A hotel account, credential right, at a client kept for the platform's own people (the console).</summary>
    public const string SignInPlatformOnly = "sign-in.platform-only-refused";
}
