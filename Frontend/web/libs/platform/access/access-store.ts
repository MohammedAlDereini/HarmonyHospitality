import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { CurrentProperty } from '@harmony/platform/context';
import { ApiClient, HttpStatusEvents } from '@harmony/platform/http';
import { SessionStore } from '@harmony/platform/session';
import { firstValueFrom } from 'rxjs';
import { AccessGrants, everyProperty, myPermissionsPath } from './access-grants';
import { Permission } from './permission';

/** After a 403 the grants are read again once; further 403s within this window do not read again. */
const FORBIDDEN_REFRESH_WINDOW_MS = 30_000;

export type AccessStatus = 'unknown' | 'loading' | 'ready' | 'unavailable';

/**
 * Presents permissions and subscription lines; never authorises (architecture 4, row platform/access). Hiding is
 * courtesy: every call is refused by the API on its own. The list lives in this tab's memory only: never in
 * storage, never in a URL; it is read again at every boot, after a separate-window sign-in, once after a 403 and
 * whenever the URL moves to another hotel (a role held at one hotel says nothing about the next).
 */
@Injectable({ providedIn: 'root' })
export class AccessStore {
  private readonly api = inject(ApiClient);
  private readonly session = inject(SessionStore);
  private readonly events = inject(HttpStatusEvents);
  private readonly property = inject(CurrentProperty);
  private readonly grantsSignal = signal<AccessGrants | null>(null);
  private readonly statusSignal = signal<AccessStatus>('unknown');
  private inFlight: Promise<void> | null = null;
  private seenResumed = 0;
  private seenForbidden = 0;
  private lastForbiddenRefreshAt = 0;

  /** null while nobody is named or the door has not answered yet. */
  readonly grants = this.grantsSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();
  /** False until the door answered: show nothing gated, as the architecture says. */
  readonly known = computed(() => this.grants() !== null);

  constructor() {
    // A separate-window sign-in finished: the person, or their roles, may differ.
    effect(() => {
      const count = this.session.resumed();
      untracked(() => {
        if (count > this.seenResumed) {
          this.seenResumed = count;
          if (this.statusSignal() !== 'unknown') void this.reload();
        }
      });
    });
    // Another hotel in the URL: the answer is per hotel (X-Harmony-Property), so it is read again there.
    let hotel = untracked(this.property.code);
    effect(() => {
      const code = this.property.code();
      untracked(() => {
        if (code !== hotel) {
          hotel = code;
          if (this.statusSignal() !== 'unknown') void this.reload();
        }
      });
    });
    // A 403 from any call: roles may have changed; read once more, then the screen shows the refusal.
    effect(() => {
      const count = this.events.forbidden();
      untracked(() => {
        if (count > this.seenForbidden) {
          this.seenForbidden = count;
          this.reloadAfterForbidden();
        }
      });
    });
  }

  /** Reads once; later callers wait for the same read. */
  ready(): Promise<void> {
    this.inFlight ??= this.read();
    return this.inFlight;
  }

  reload(): Promise<void> {
    this.inFlight = null;
    return this.ready();
  }

  /** May the person do this, here? The answer is for the hotel in the URL (tenant-wide roles plus the hotel's own). */
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

  private async read(): Promise<void> {
    this.statusSignal.set('loading');
    const result = await firstValueFrom(this.api.get<readonly number[]>(myPermissionsPath));
    if (result.kind === 'ok') {
      this.grantsSignal.set({ permissions: new Set(result.data), modules: {} });
      this.statusSignal.set('ready');
      return;
    }
    this.grantsSignal.set(null);
    this.statusSignal.set('unavailable');
  }

  private reloadAfterForbidden(): void {
    const now = Date.now();
    if (now - this.lastForbiddenRefreshAt < FORBIDDEN_REFRESH_WINDOW_MS) return;
    this.lastForbiddenRefreshAt = now;
    if (this.statusSignal() !== 'unknown') void this.reload();
  }
}
