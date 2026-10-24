/** Identity's PrivilegeLevel, sent by name: a person's standing is the highest level among their roles. */
export const PrivilegeLevel = {
  standard: 'Standard',
  supervisor: 'Supervisor',
  manager: 'Manager',
  administrator: 'Administrator',
} as const;

export type PrivilegeLevel = (typeof PrivilegeLevel)[keyof typeof PrivilegeLevel];

/** One row of POST /api/Role/get-all (RoleListModel); only what screens read. */
export interface Role {
  readonly id: string;
  readonly code: string;
  readonly nameEn: string;
  readonly nameAr: string | null;
  readonly privilegeLevel: PrivilegeLevel;
  /** Shipped with the platform: never edited or deleted. */
  readonly isSystemRole: boolean;
  readonly isSuperRole: boolean;
}

/** The role's name in the person's language; Arabic falls back to English when none was given. */
export const roleName = (role: Pick<Role, 'nameEn' | 'nameAr'>, locale: string): string =>
  locale.startsWith('ar') && role.nameAr !== null && role.nameAr !== '' ? role.nameAr : role.nameEn;

/** What the role editor writes (CreateRoleCommand / EditRoleCommand); the Arabic name may be empty. */
export interface RoleDraft {
  readonly code: string;
  readonly nameEn: string;
  readonly nameAr: string;
  readonly privilegeLevel: PrivilegeLevel;
}

/** One entry of GET /api/Role/permissions (PermissionModel): the catalogue a role is built from. */
export interface PermissionEntry {
  readonly id: string;
  /** The PermissionEnum member's name, e.g. "ViewUsers". */
  readonly permissionValue: string;
  readonly name: string;
  readonly description: string | null;
}

/** GET /api/Role/{id} (RoleDetailModel): the row plus the permissions it holds. */
export interface RoleDetail extends Role {
  readonly permissions: readonly PermissionEntry[];
}

/** What an administrator gives a new account; Identity mails the invitation, nobody types a password. */
export interface NewAccount {
  readonly displayName: string;
  readonly email: string;
  readonly roleId: string;
}
