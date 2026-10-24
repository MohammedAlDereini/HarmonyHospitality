import { Routes } from '@angular/router';
import { sessionReady, sessionRoutes } from '@harmony/platform/session';
import { Permission, allowed } from '@harmony/platform/access';
import {
  PropertyChooser,
  propertyAllowed,
  providePropertyContext,
} from '@harmony/platform/context';

// The shape every screen follows (architecture 10): the session pages outside, everything else inside the shell.
export const routes: Routes = [
  ...sessionRoutes,
  {
    path: '',
    canMatch: [sessionReady],
    loadComponent: async () => (await import('@harmony/ui/shell')).Shell,
    children: [
      {
        path: '',
        pathMatch: 'full',
        component: PropertyChooser,
        title: $localize`:@@platform.context.chooser.page-title:Properties`,
      },
      { path: 'hello', loadComponent: async () => (await import('./hello')).Hello },
      {
        path: 'p/:property',
        canMatch: [propertyAllowed],
        providers: [providePropertyContext()],
        children: [
          {
            path: 'admin',
            canMatch: [allowed(Permission.ViewUsers)],
            data: { crumb: $localize`:@@administration.crumb:Administration` },
            loadChildren: async () =>
              (await import('@harmony/administration/feature-users')).routes,
          },
          { path: '', pathMatch: 'full', redirectTo: 'admin' },
        ],
      },
    ],
  },
  { path: '**', loadComponent: async () => (await import('@harmony/ui/shell')).NotFoundPage },
];
