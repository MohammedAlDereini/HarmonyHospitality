/** One line of GET /api/UserAccount/{id}/security-events (SecurityEventModel): newest first, 100 at most. */
export interface SecurityEvent {
  readonly action: string;
  readonly occurredAtUtc: string;
  readonly actorSubject: string;
  readonly sourceAddress: string | null;
  readonly device: string | null;
  readonly detail: string | null;
}

/** One line of GET /api/UserAccount/{id}/devices (UserDeviceModel). */
export interface Device {
  readonly id: string;
  readonly label: string;
  readonly firstSeenUtc: string;
  readonly lastSeenUtc: string;
  readonly lastAddress: string | null;
  readonly signedIn: boolean;
}

/** One line of GET /api/UserAccount/{id}/passkeys (UserPasskeyModel); only what the screen shows. */
export interface Passkey {
  readonly id: string;
  readonly name: string;
  readonly createdUtc: string;
  readonly lastUsedUtc: string | null;
  readonly isBackedUp: boolean;
}

/** One line of GET /api/UserAccount/{id}/access (RoleAssignmentModel): a role held at one hotel or one group. */
export interface HotelRoleAssignment {
  readonly id: string;
  readonly roleId: string;
  readonly roleCode: string;
  readonly propertyId: string | null;
  readonly propertyGroupId: string | null;
  /** The hotel's or the group's code. */
  readonly scopeCode: string;
  readonly endsAtUtc: string | null;
  readonly isCurrent: boolean;
}

/** One line of the same read (AccessGrantModel): a role lent for a window, with the reason, 30 days at most. */
export interface HotelAccessGrant {
  readonly id: string;
  readonly roleId: string;
  readonly roleCode: string;
  readonly propertyId: string | null;
  readonly propertyGroupId: string | null;
  readonly scopeCode: string;
  readonly fromUtc: string;
  readonly toUtc: string;
  readonly revokedAtUtc: string | null;
  readonly grantedById: string;
  readonly reason: string;
  readonly isActive: boolean;
}

/** What gives an account access beyond its tenant-wide roles (AccountAccessModel), current first. */
export interface AccountAccess {
  readonly id: string;
  readonly assignments: readonly HotelRoleAssignment[];
  readonly grants: readonly HotelAccessGrant[];
}

/** Where a role is given (AssignmentScope): exactly one of the two. */
export interface AccessScope {
  readonly propertyId: string | null;
  readonly propertyGroupId: string | null;
}

/** One row of POST /api/PropertyGroup/mine (PropertyGroupModel); only what the chooser shows. */
export interface GroupChoice {
  readonly id: string;
  readonly code: string;
  readonly typeCode: string;
  readonly nameEn: string;
  readonly nameAr: string;
}

/** The group's name in the person's language; Arabic falls back to English when none was given. */
export const groupName = (group: Pick<GroupChoice, 'nameEn' | 'nameAr'>, locale: string): string =>
  locale.startsWith('ar') && group.nameAr !== '' ? group.nameAr : group.nameEn;
