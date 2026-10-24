import { DOCUMENT, Injectable, computed, inject } from '@angular/core';
import { RUNTIME_CONFIG } from '@harmony/platform/config';
import { FlagsStore } from '@harmony/platform/flags';
import { APP_VERSION, compareVersions } from '@harmony/shared/domain';

/** The deployment says no client older than this may keep running; 0 and empty mean no floor. */
const noFloor = (floor: string | null | undefined): boolean =>
  floor === null || floor === undefined || floor.trim() === '' || floor.trim() === '0';

/**
 * Whether this build may keep running (architecture D38, app-update): the page's config element says the floor at
 * boot, the flags door says it again on every read, so a floor raised after boot is seen without a reload. Below the
 * floor the shell asks for a reload; a newer release that is merely available is only offered.
 */
@Injectable({ providedIn: 'root' })
export class AppUpdate {
  private readonly config = inject(RUNTIME_CONFIG);
  private readonly flags = inject(FlagsStore);
  private readonly document = inject(DOCUMENT);

  /** The running build is older than the floor: work can no longer be trusted to the old code. */
  readonly stale = computed(() => {
    const floors = [this.config.minimumClientVersion, this.flags.minimumClientVersion()];
    return floors.some((floor) => !noFloor(floor) && compareVersions(APP_VERSION, floor ?? '') < 0);
  });

  /** A newer release is served than the one this page booted with. */
  readonly newer = computed(() => {
    const served = this.flags.release();
    return served !== null && served !== this.config.release;
  });

  reload(): void {
    this.document.location.reload();
  }
}
