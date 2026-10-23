import { Routes } from '@angular/router';
import { signedInRoute, signedOutRoute } from './session-paths';

/** The routes every app registers, never behind a guard: the separate window's landing page, the signed-out page. */
export const sessionRoutes: Routes = [
  {
    path: signedInRoute,
    loadComponent: async () => (await import('./signed-in-page')).SignedInPage,
  },
  {
    path: signedOutRoute,
    loadComponent: async () => (await import('./signed-out-page')).SignedOutPage,
  },
];
