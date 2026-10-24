import { InjectionToken } from '@angular/core';
import { SessionUser } from '@harmony/platform/session';
import { Permission } from './permission';

/** The chain-wide key in the modules map: lines held for every property. */
export const everyProperty = '*';

/** What the person may do and which subscription lines they hold, as the session payload will say (architecture 11.3). */
export interface AccessGrants {
  readonly permissions: ReadonlySet<Permission>;
  /** Module codes per property code, plus "*" for chain-wide lines. */
  readonly modules: Readonly<Record<string, readonly string[]>>;
}

/**
 * Reads the grants out of what the BFF answered. Until the session payload exists (D2, the backend decides its names)
 * the answer is null: "unknown", and nothing permission-gated shows beyond identity self-service (11.3). Tests
 * replace this to prove the positive paths.
 */
export const ACCESS_GRANTS = new InjectionToken<(user: SessionUser) => AccessGrants | null>(
  'ACCESS_GRANTS',
  { providedIn: 'root', factory: () => () => null },
);
