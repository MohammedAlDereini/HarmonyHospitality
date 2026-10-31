/**
 * One line of the platform's security trail across every customer (POST /api/SecurityAudit/get-all,
 * PlatformTrailEventModel): the row itself and the names Identity reads beside it. Never an e-mail, never a secret.
 */
export interface TrailEvent {
  readonly id: string;
  readonly tenantId: string;
  readonly tenantCode: string | null;
  readonly tenantNameEn: string | null;
  readonly tenantNameAr: string | null;
  /** The account the event is about. */
  readonly userAccountId: string;
  readonly userDisplayName: string | null;
  /** "tenant.suspended", "sign-in.passkey": Identity's SecurityAuditActions. */
  readonly action: string;
  /** Who did it: an account id, the account itself, or "system". */
  readonly actorSubject: string;
  readonly actorDisplayName: string | null;
  readonly sourceAddress: string | null;
  readonly device: string | null;
  readonly occurredAtUtc: string;
  readonly detail: string | null;
}

/** How often one action was written inside a window (GET /api/SecurityAudit/counts, TrailCountModel). */
export interface TrailCount {
  readonly action: string;
  readonly count: number;
}

/** What the trail is narrowed by; page is 1-based; instants are ISO strings in UTC. */
export interface TrailFilters {
  readonly search: string;
  /** One action, or a family by its prefix ("sign-in."); null: all. */
  readonly action: string | null;
  /** Only these actions by name (an outcome is several); empty: no narrowing. */
  readonly actions: readonly string[];
  readonly tenantId: string | null;
  readonly userAccountId: string | null;
  /** Only what one actor did: a platform person's account id, or "system"; null for everyone. */
  readonly actorSubject: string | null;
  readonly from: string | null;
  readonly to: string | null;
  readonly page: number;
}

/** The actor Identity writes when nobody acted: the service itself. */
export const SYSTEM_ACTOR = 'system';

/** The families of the trail, by prefix, as the Audit log's event filter offers them. */
export const TRAIL_FAMILIES = [
  'sign-in.',
  'account.',
  'tenant.',
  'property.',
  'property-group.',
  'password.',
  'two-step.',
  'passkey.',
  'session.',
  'device.',
  'company-login.',
  'directory.',
  'approval.',
  'config.',
  'access.',
  'identity.',
] as const;

export type TrailFamily = (typeof TRAIL_FAMILIES)[number];

/** Every sign-in, got in or refused. */
export const SIGN_IN_FAMILY: TrailFamily = 'sign-in.';

/** The sign-ins that got in: the person is through the door. */
const signedIn: ReadonlySet<string> = new Set([
  'sign-in.password-only',
  'sign-in.two-step',
  'sign-in.email-link',
  'sign-in.after-two-step-setup',
  'sign-in.passkey',
  'sign-in.company',
  'sign-in.password-grant',
  'sign-in.password-grant-two-step',
]);

/** A sign-in that stopped before the door: a wrong password or code, a lock-out, a closed account or customer, a refusal. */
const refused: ReadonlySet<string> = new Set([
  'sign-in.wrong-password',
  'sign-in.wrong-code',
  'sign-in.locked-out',
  'sign-in.account-not-active',
  'sign-in.tenant-not-active',
  'sign-in.passkey-refused',
  'sign-in.platform-only-refused',
  'sign-in.company-refused',
]);

/** A sign-in that waits for the person: "Trust device" in the e-mail (L16). */
const held: ReadonlySet<string> = new Set(['sign-in.new-device-held']);

export type SignInOutcome = 'signed-in' | 'refused' | 'held' | 'other';

const outcomes: readonly (readonly [ReadonlySet<string>, SignInOutcome])[] = [
  [signedIn, 'signed-in'],
  [refused, 'refused'],
  [held, 'held'],
];

/** What a sign-in row came to, by its action name. */
export const signInOutcomeOf = (action: string): SignInOutcome =>
  outcomes.find(([actions]) => actions.has(action))?.[1] ?? 'other';

/** The family a row belongs to: the text before and including the first dot, or the whole action when it has none. */
export const familyOf = (action: string): string => {
  const dot = action.indexOf('.');
  return dot < 0 ? action : action.slice(0, dot + 1);
};
