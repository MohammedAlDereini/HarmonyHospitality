/** One row of GET /api/Approval/pending (ApprovalRequestModel): a change waiting for a second administrator. */
export interface ApprovalRequest {
  readonly id: string;
  /** What waits (ApprovalKinds), worded by approvalKindName. */
  readonly kind: string;
  readonly requestedById: string;
  readonly requestedBy: string | null;
  readonly targetId: string | null;
  readonly target: string | null;
  readonly raisedAtUtc: string;
}

/** The customer's own rules, read from GET /api/Tenant/mine (TenantModel). */
export interface CustomerRules {
  readonly approvalsRequired: boolean;
  /** Days without a sign-in before an account is suspended: 1 to 90. */
  readonly inactivityDays: number;
  /** The Mews gate: a sign-in from an untrusted browser waits for "Trust device" in the e-mail. */
  readonly trustedDevicesRequired: boolean;
}

/** The longest the inactivity rule may wait (PCI DSS 8.2.6): a customer may choose fewer days, never more. */
export const MAX_INACTIVITY_DAYS = 90;

/** One row of GET /api/Invitation/mine (InvitationModel): an invitation still waiting for a first password. */
export interface Invitation {
  readonly id: string;
  readonly userAccountId: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly sentBy: string | null;
  readonly sentAtUtc: string;
  readonly expiresAtUtc: string;
  readonly isExpired: boolean;
}

/** One row of GET /api/UserAccount/{id}/sessions (SessionRecordModel): one sign-in of the person. */
export interface SessionRecord {
  readonly startedAtUtc: string;
  readonly endedAtUtc: string | null;
  readonly endReason: string | null;
  /** How the person proved it, as Identity records it ("pwd", "pwd,otp", "external,mfa", …). */
  readonly methods: string;
  /** Where: "local", "Microsoft", "Google". */
  readonly provider: string;
  readonly multiFactorSatisfied: boolean;
}

/** A group the company directory pushed, with the role Harmony gives its members (DirectoryGroupModel). */
export interface DirectoryGroup {
  readonly id: string;
  readonly displayName: string;
  readonly externalId: string | null;
  /** Null: the group gives nothing. */
  readonly roleId: string | null;
  readonly propertyId: string | null;
  readonly propertyGroupId: string | null;
  readonly members: number;
}

/** GET /api/Directory/mine (DirectoryModel): the customer's company directory connection; null data when there is none. */
export interface DirectoryConnection {
  readonly id: string;
  readonly clientId: string;
  readonly isEnabled: boolean;
  readonly secretIssuedAtUtc: string;
  /** The address the directory calls (Entra's "Tenant URL"). */
  readonly scimUrl: string;
  /** The token door (Entra's "Token endpoint", OAuth 2.0 client credentials). */
  readonly tokenUrl: string;
  readonly people: number;
  readonly groups: readonly DirectoryGroup[];
}

/** Shown once, when the connection opens or the secret is replaced (DirectorySecretModel). Never stored by the app. */
export interface DirectorySecret {
  readonly clientId: string;
  readonly secret: string;
  /** "clientId.secret": the long token for Okta's header login and Entra's "Secret Token". */
  readonly secretToken: string;
  readonly scimUrl: string;
  readonly tokenUrl: string;
}

/** What a waiting request is about, in the reader's words, per ApprovalKinds value. */
const approvalKindNames: Readonly<Record<string, string>> = {
  'account.administrator-roles': $localize`:@@administration.approvals.kind.administrator-roles:An administrator role`,
  'account.administrator-assignment': $localize`:@@administration.approvals.kind.administrator-assignment:An administrator role at a hotel`,
  'account.two-step-exemption': $localize`:@@administration.approvals.kind.two-step-exemption:Two-step exception`,
  'account.merge': $localize`:@@administration.approvals.kind.merge:Merging two accounts`,
  'company-login.switch-off': $localize`:@@administration.approvals.kind.company-login-off:Switching company sign-in off`,
  'approvals.switch-off': $localize`:@@administration.approvals.kind.approvals-off:Switching approvals off`,
  'directory.administrator-group': $localize`:@@administration.approvals.kind.directory-administrator:An administrator role for a directory group`,
};

/** A kind Identity adds later than this app shows its own name until the app learns it. */
export const approvalKindName = (kind: string): string => approvalKindNames[kind] ?? kind;
