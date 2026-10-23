import { Routes } from '@angular/router';
import { sessionReady, sessionRoutes } from '@harmony/platform/session';

// The shape every screen follows (architecture 10): the session pages outside, everything else inside the shell.
export const routes: Routes = [
  ...sessionRoutes,
  {
    path: '',
    canMatch: [sessionReady],
    loadComponent: async () => (await import('@harmony/ui/shell')).Shell,
    children: [
      { path: '', loadComponent: async () => (await import('./home')).Home },
      { path: 'hello', loadComponent: async () => (await import('./hello')).Hello },
    ],
  },
  { path: '**', loadComponent: async () => (await import('@harmony/ui/shell')).NotFoundPage },
];
