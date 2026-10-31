import { Routes } from '@angular/router';
import { signedInRoute, signedOutRoute } from './session-paths';

/**
 * The routes every app registers, never behind a guard: the separate window's landing page, the signed-out page,
 * and the three password pages the sign-in page and the mails point at ("forgot", the reset link, the invitation).
 */
export const sessionRoutes: Routes = [
  {
    path: 'forgot-password',
    title: $localize`:@@platform.session.forgot.page-title:Forgot your password`,
    loadComponent: async () => (await import('./forgot-password-page')).ForgotPasswordPage,
  },
  {
    path: 'account/reset-password',
    title: $localize`:@@platform.session.set-password.page-title-reset:Choose a new password`,
    data: { mode: 'reset' },
    loadComponent: async () => (await import('./set-password-page')).SetPasswordPage,
  },
  {
    path: 'account/set-password',
    title: $localize`:@@platform.session.set-password.page-title-set:Choose your password`,
    data: { mode: 'set' },
    loadComponent: async () => (await import('./set-password-page')).SetPasswordPage,
  },
  {
    path: signedInRoute,
    loadComponent: async () => (await import('./signed-in-page')).SignedInPage,
  },
  {
    path: signedOutRoute,
    loadComponent: async () => (await import('./signed-out-page')).SignedOutPage,
  },
];
