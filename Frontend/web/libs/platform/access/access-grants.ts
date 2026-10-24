/** The chain-wide key in the modules map: lines held for every property. */
export const everyProperty = '*';

/** Identity's self-service door: the caller's effective permission values, the same set the API refuses with. */
export const myPermissionsPath = '/api/UserAccount/MyPermissions';

/** What the person may do and which subscription lines they hold (architecture 11.3). */
export interface AccessGrants {
  /** PermissionEnum values; numbers the catalogue does not know are simply never asked for. */
  readonly permissions: ReadonlySet<number>;
  /** Module codes per property code, plus "*" for chain-wide lines. Empty until the subscription payload exists. */
  readonly modules: Readonly<Record<string, readonly string[]>>;
}
