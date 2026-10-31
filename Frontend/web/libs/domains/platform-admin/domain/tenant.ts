/** Identity's TenantStatus, sent by name. */
export const TenantStatus = {
  active: 'Active',
  suspended: 'Suspended',
  terminated: 'Terminated',
} as const;

export type TenantStatus = (typeof TenantStatus)[keyof typeof TenantStatus];

/** Tenant.MinTier .. Tenant.MaxTier in Identity. */
export const TENANT_MIN_TIER = 1;
export const TENANT_MAX_TIER = 5;

/** One line of a subscription (SubscriptionLineModel): a module bought by a tenant, for one property or for all. */
export interface SubscriptionLine {
  readonly id: string;
  readonly tenantId: string;
  readonly propertyId: string | null;
  readonly moduleCode: string;
  readonly isEnabled: boolean;
  readonly disabledReason: string | null;
  readonly renewsAutomatically: boolean;
  readonly rooms: number | null;
  readonly terminals: number | null;
  readonly startsOn: string;
  readonly endsOn: string | null;
  readonly isHeld: boolean;
}

/** Identity's AdministrativeState of an account, sent by name. */
export type AdministratorState = 'Active' | 'Suspended' | 'Terminated';

/** The account holding the customer's ADMINISTRATOR role (TenantAdministratorModel), and where its invitation stands. */
export interface TenantAdministrator {
  readonly userAccountId: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly state: AdministratorState;
  /** The newest invitation is still open: no first password yet. */
  readonly invitationPending: boolean;
  /** The newest invitation ran out unredeemed. */
  readonly invitationExpired: boolean;
  readonly invitedOn: string | null;
  /** Two-step sign-in is set up on the account, and which second step. */
  readonly twoStepEnrolled: boolean;
  readonly twoStepMethod: string;
  /** The last session's start and methods; null when never signed in, or on the list. */
  readonly lastSignInUtc: string | null;
  readonly lastSignInMethods: string | null;
}

/** One row of POST /api/Tenant/get-all and GET /api/Tenant/{id} (TenantModel); only what screens read. */
export interface Tenant {
  readonly id: string;
  readonly code: string;
  readonly externalIdentifier: string | null;
  readonly legalNameEn: string;
  readonly legalNameAr: string;
  readonly countryCode: string | null;
  readonly defaultCurrency: string | null;
  readonly status: TenantStatus;
  readonly statusReason: string | null;
  readonly tier: number;
  readonly onboardedOn: string;
  /** The one platform tenant: us, never a customer. */
  readonly isPlatform: boolean;
  readonly subscriptions: readonly SubscriptionLine[];
  /** The list's company (C2): hotels in every state, rooms declared by the active ones, rooms licensed today. */
  readonly hotels: number;
  readonly roomsInUse: number;
  readonly licensedRooms: number;
  /** Module codes with an enabled line, each once, in order. */
  readonly moduleCodes: readonly string[];
  /** The customer's people, how many keep a password, how many are on the two-step exception list. */
  readonly people: number;
  readonly peopleWithPassword: number;
  readonly twoStepExceptions: number;
  /** Null when the customer has no administrator account (none yet, or born before invitations were kept). */
  readonly administrator: TenantAdministrator | null;
}

/** The tenant's name in the person's language. */
export const tenantName = (
  tenant: Pick<Tenant, 'legalNameEn' | 'legalNameAr'>,
  locale: string,
): string =>
  locale.startsWith('ar') && tenant.legalNameAr !== '' ? tenant.legalNameAr : tenant.legalNameEn;

/** What the tenant editor writes (CreateTenantCommand / EditTenantCommand). */
export interface TenantDraft {
  readonly code: string;
  readonly legalNameEn: string;
  readonly legalNameAr: string;
  readonly countryCode: string;
  readonly defaultCurrency: string;
  readonly tier: number;
  readonly externalIdentifier: string;
  /** The first administrator, invited by mail; only on creation. */
  readonly administratorEmail: string;
  readonly administratorDisplayName: string;
}

/** What is asked for when a module is subscribed (SubscribeCommand). */
export interface SubscriptionDraft {
  readonly propertyId: string | null;
  readonly moduleCode: string;
  readonly rooms: number | null;
  readonly terminals: number | null;
  readonly startsOn: string;
  readonly endsOn: string | null;
  readonly renewsAutomatically: boolean;
}

/** What the tenant list is filtered by (the console's facets); page is 1-based. */
export interface TenantFilters {
  readonly search: string;
  /** Empty: every status. */
  readonly statuses: readonly TenantStatus[];
  readonly tier: number | null;
  readonly countryCode: string | null;
  readonly moduleCode: string | null;
  readonly page: number;
}
