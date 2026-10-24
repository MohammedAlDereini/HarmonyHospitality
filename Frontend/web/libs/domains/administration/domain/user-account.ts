/** Identity's AdministrativeState, sent as a number (UserAccountModel.State). */
const ACTIVE = 1;
const SUSPENDED = 2;
const TERMINATED = 3;

export const AccountState = {
  active: ACTIVE,
  suspended: SUSPENDED,
  terminated: TERMINATED,
} as const;

export type AccountState = (typeof AccountState)[keyof typeof AccountState];

/** One row of POST /api/UserAccount/get-all (UserAccountModel); only what screens read. */
export interface UserAccount {
  readonly id: string;
  readonly userName: string;
  readonly email: string | null;
  readonly displayName: string;
  readonly twoFactorEnabled: boolean;
  /** Set when the person is on the two-step exception list. */
  readonly twoStepExemptReason: string | null;
  readonly state: AccountState;
  readonly stateReason: string | null;
  /** As Identity sends it; shown in the property's zone. */
  readonly createdDate: string;
}

/** What the list is filtered by; page is 1-based. */
export interface UserFilters {
  readonly search: string;
  readonly page: number;
}
