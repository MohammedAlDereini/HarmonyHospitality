import { Routes } from '@angular/router';
import { signedInRoute } from './session-paths';

/** The routes every app registers: the landing page of a separate-window sign-in. Never behind a guard. */
export const sessionRoutes: Routes = [
  {
    path: signedInRoute,
    loadComponent: async () => (await import('./signed-in-page')).SignedInPage,
  },
];
