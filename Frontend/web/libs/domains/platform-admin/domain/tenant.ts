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

/** What the tenant list is filtered by; page is 1-based. */
export interface TenantFilters {
  readonly search: string;
  readonly status: TenantStatus | null;
  readonly page: number;
}
