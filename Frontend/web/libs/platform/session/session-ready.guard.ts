import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { SessionStore } from './session-store';

/**
 * Matches a route only for a signed-in person (architecture 10). Signed out: a top-level walk to the sign-in door
 * with the attempted URL as the return path. Unavailable: nothing matches; the shell says the BFF is not responding.
 */
export const sessionReady: CanMatchFn = async () => {
  const store = inject(SessionStore);
  const router = inject(Router);
  const attempted = router.currentNavigation()?.extractedUrl;
  const state = await store.ready();
  if (state === 'signed-in') return true;
  if (state === 'signed-out' || state === 'expired') {
    store.signIn(attempted === undefined ? '/' : router.serializeUrl(attempted));
  }
  return false;
};
