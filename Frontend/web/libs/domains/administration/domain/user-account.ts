/** Identity's AdministrativeState, sent by name (the platform serialises enums as strings). */
export const AccountState = {
  active: 'Active',
  suspended: 'Suspended',
  terminated: 'Terminated',
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
  /** Stage E: the company directory owns this person; name, state and roles change only there. */
  readonly isDirectoryManaged: boolean;
  /** As Identity sends it; shown in the property's zone. */
  readonly createdDate: string;
  /** The newest sign-in, as Identity recorded it; null when the person never signed in. */
  readonly lastSignInUtc: string | null;
  /** The device that was seen last ("Windows · Edge 130"); null when none was. */
  readonly lastSignInDevice: string | null;
  /** The roles the account holds. */
  readonly roleIds: readonly string[];
}

/** The Two-step facet's three answers. */
export type TwoStepFilter = 'on' | 'off' | 'exempt';

/** What the list is filtered by; page is 1-based. */
export interface UserFilters {
  readonly search: string;
  readonly state: AccountState | null;
  readonly twoStep: TwoStepFilter | null;
  readonly roleId: string | null;
  readonly page: number;
}
