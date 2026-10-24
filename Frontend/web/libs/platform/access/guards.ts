import { inject } from '@angular/core';
import { CanMatchFn } from '@angular/router';
import { SessionStore } from '@harmony/platform/session';
import { AccessStore } from './access-store';
import { Permission } from './permission';

/** The route exists for the person only with this permission (architecture 10: guards present, never protect). */
export const allowed =
  (permission: Permission): CanMatchFn =>
  async () => {
    const session = inject(SessionStore);
    const access = inject(AccessStore);
    await session.ready();
    await access.ready();
    return access.can(permission);
  };

/** The route exists only where the customer holds the module's line for that property (or chain-wide). */
export const moduleEntitled =
  (module: string): CanMatchFn =>
  async (_route, _segments, snapshot) => {
    const session = inject(SessionStore);
    const access = inject(AccessStore);
    await session.ready();
    await access.ready();
    const property: unknown = snapshot.params['property'];
    return typeof property === 'string' && access.entitled(module, property);
  };
