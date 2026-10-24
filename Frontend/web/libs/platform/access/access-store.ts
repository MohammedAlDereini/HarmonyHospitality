import { Injectable, computed, inject } from '@angular/core';
import { SessionStore } from '@harmony/platform/session';
import { ACCESS_GRANTS, everyProperty } from './access-grants';
import { Permission } from './permission';

/**
 * Presents permissions and subscription lines; never authorises (architecture 4, row platform/access). Hiding is
 * courtesy: every call is refused by the API on its own. Unknown grants mean "show nothing gated".
 */
@Injectable({ providedIn: 'root' })
export class AccessStore {
  private readonly session = inject(SessionStore);
  private readonly grantsOf = inject(ACCESS_GRANTS);

  /** null while nobody is named or the payload does not exist yet (D2). */
  readonly grants = computed(() => {
    const user = this.session.user();
    return user === null ? null : this.grantsOf(user);
  });

  /** False until the session payload exists (D2). */
  readonly known = computed(() => this.grants() !== null);

  /** May the person do this? Roles are tenant-wide today; when a property narrows the answer, it becomes a parameter. */
  can(permission: Permission): boolean {
    return this.grants()?.permissions.has(permission) ?? false;
  }

  /** Does the customer hold this module's line for the property, or chain-wide? */
  entitled(module: string, property: string): boolean {
    const modules = this.grants()?.modules;
    if (modules === undefined) return false;
    const held = (code: string): boolean => modules[code]?.includes(module) ?? false;
    return held(property) || held(everyProperty);
  }
}
