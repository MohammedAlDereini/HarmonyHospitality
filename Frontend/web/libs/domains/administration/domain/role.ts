/** One row of POST /api/Role/get-all (RoleListModel); only what the account form reads. */
export interface RoleOption {
  readonly id: string;
  readonly code: string;
  readonly nameEn: string;
  readonly nameAr: string | null;
}

/** The role's name in the person's language; Arabic falls back to English when none was given. */
export const roleName = (role: RoleOption, locale: string): string =>
  locale.startsWith('ar') && role.nameAr !== null && role.nameAr !== '' ? role.nameAr : role.nameEn;

/** What an administrator gives a new account; Identity mails the invitation, nobody types a password. */
export interface NewAccount {
  readonly displayName: string;
  readonly email: string;
  readonly roleId: string;
}
