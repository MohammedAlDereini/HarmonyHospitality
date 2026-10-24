import { httpResource } from '@angular/common/http';
import { Injectable, Signal, computed, inject } from '@angular/core';
import { SessionStore } from '@harmony/platform/session';

/** What /bff/flags answers: the ring this host serves, the release, and each flag's state for that ring (D37, D38). */
export interface FlagsAnswer {
  readonly ring: string;
  readonly release: string;
  /** The oldest client build allowed to keep running; empty for no floor. */
  readonly minimumClientVersion: string;
  readonly flags: Readonly<Record<string, boolean>>;
}

export const flagsPath = '/bff/flags';

/**
 * The feature flags of this deployment (architecture D37): read once a session exists, read again on demand. A flag
 * that is unknown, or a door that does not answer, is OFF: a feature never shows by accident (fail closed).
 */
@Injectable({ providedIn: 'root' })
export class FlagsStore {
  private readonly session = inject(SessionStore);
  private readonly answer = httpResource<FlagsAnswer>(() =>
    this.session.user() === null ? undefined : { url: flagsPath },
  );

  /** The last good answer; a resource in error throws on value(), so the error state reads as no answer at all. */
  private readonly current = computed(() =>
    this.answer.hasValue() ? this.answer.value() : undefined,
  );

  readonly ring = computed(() => this.current()?.ring ?? null);
  readonly release = computed(() => this.current()?.release ?? null);
  readonly minimumClientVersion = computed(() => this.current()?.minimumClientVersion ?? null);
  readonly known = computed(() => this.current() !== undefined);

  /** True while the named flag is on for this ring; false when off, unknown, or not yet read. */
  on(name: string): Signal<boolean> {
    return computed(() => this.current()?.flags[name] === true);
  }

  reload(): void {
    this.answer.reload();
  }
}
