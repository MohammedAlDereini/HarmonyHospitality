import { DOCUMENT, Injectable, inject } from '@angular/core';

/** One named window: a second press focuses the same sign-in window instead of opening another. */
const SIGN_IN_WINDOW_NAME = 'harmony-sign-in';
const SIGN_IN_WINDOW_FEATURES = 'popup=yes,width=520,height=720';

/** The two ways the app leaves itself. Tests replace this; nothing else touches window.location or window.open. */
@Injectable({ providedIn: 'root' })
export class BrowserNavigation {
  private readonly window = inject(DOCUMENT).defaultView;

  /** A top-level navigation: this page, and everything it holds in memory, is gone. */
  go(localPath: string): void {
    this.window?.location.assign(localPath);
  }

  /** A separate window the person asked for with a click (so no popup blocker); null when the browser refused. */
  open(localPath: string): Window | null {
    return this.window?.open(localPath, SIGN_IN_WINDOW_NAME, SIGN_IN_WINDOW_FEATURES) ?? null;
  }
}
