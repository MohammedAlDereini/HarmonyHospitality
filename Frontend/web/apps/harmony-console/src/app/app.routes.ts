import { Routes } from '@angular/router';
import { Permission, allowed } from '@harmony/platform/access';
import { sessionReady, sessionRoutes } from '@harmony/platform/session';

// The shape every screen follows (architecture 10): the session pages outside, everything else inside the shell.
// The console is the platform tenant's own product: the platform's things, then its own people and roles.
export const routes: Routes = [
  ...sessionRoutes,
  {
    path: '',
    canMatch: [sessionReady],
    loadComponent: async () => (await import('@harmony/ui/shell')).Shell,
    children: [
      { path: '', loadComponent: async () => (await import('./home')).Home },
      { path: 'hello', loadComponent: async () => (await import('./hello')).Hello },
      {
        path: 'tenants',
        canMatch: [allowed(Permission.ViewTenants)],
        loadChildren: async () => (await import('@harmony/platform-admin/feature-tenants')).routes,
      },
      {
        path: 'properties',
        canMatch: [allowed(Permission.ViewTenants)],
        loadChildren: async () =>
          (await import('@harmony/platform-admin/feature-properties')).routes,
      },
      {
        path: 'groups',
        canMatch: [allowed(Permission.ViewTenants)],
        loadChildren: async () => (await import('@harmony/platform-admin/feature-groups')).routes,
      },
      {
        path: 'reference',
        canMatch: [allowed(Permission.ViewTenants)],
        loadChildren: async () =>
          (await import('@harmony/platform-admin/feature-reference-data')).routes,
      },
      {
        path: 'people',
        canMatch: [allowed(Permission.ViewUsers)],
        data: { crumb: $localize`:@@administration.crumb:Administration` },
        loadChildren: async () => (await import('@harmony/administration/feature-users')).routes,
      },
      {
        path: 'roles',
        canMatch: [allowed(Permission.ViewRoles)],
        data: { crumb: $localize`:@@administration.crumb:Administration` },
        loadChildren: async () => (await import('@harmony/administration/feature-roles')).routes,
      },
    ],
  },
  { path: '**', loadComponent: async () => (await import('@harmony/ui/shell')).NotFoundPage },
];
