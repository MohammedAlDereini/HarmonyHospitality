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
    loadComponent: async () => (await import('@harmony/ui/console-shell')).ConsoleShell,
    children: [
      { path: '', loadComponent: async () => (await import('./home/home')).Home },
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
      // Onboarding is the new-tenant wizard (C5): the rail's link opens it.
      { path: 'onboarding', redirectTo: '/tenants/new' },
      {
        path: 'trail',
        canMatch: [allowed(Permission.ViewTenants)],
        data: { crumb: $localize`:@@harmony-console.crumb.operations:Operations` },
        loadChildren: async () =>
          (await import('@harmony/platform-operations/feature-trail')).routes,
      },
      {
        path: 'sign-ins',
        canMatch: [allowed(Permission.ViewTenants)],
        data: { crumb: $localize`:@@harmony-console.crumb.operations:Operations` },
        loadChildren: async () =>
          (await import('@harmony/platform-operations/feature-sign-ins')).routes,
      },
      {
        path: 'health',
        canMatch: [allowed(Permission.ViewSettings)],
        data: { crumb: $localize`:@@harmony-console.crumb.operations:Operations` },
        loadChildren: async () =>
          (await import('@harmony/platform-operations/feature-health')).routes,
      },
      {
        path: 'settings',
        canMatch: [allowed(Permission.ViewSettings)],
        data: { crumb: $localize`:@@administration.crumb:Administration` },
        loadChildren: async () => (await import('./settings/routes')).consoleRoutes,
      },
      {
        path: 'people',
        canMatch: [allowed(Permission.ViewUsers)],
        data: { crumb: $localize`:@@administration.crumb:Administration` },
        loadChildren: async () => (await import('./people/routes')).consoleRoutes,
      },
      {
        path: 'roles',
        canMatch: [allowed(Permission.ViewRoles)],
        data: { crumb: $localize`:@@administration.crumb:Administration` },
        loadChildren: async () => (await import('./roles/routes')).consoleRoutes,
      },
    ],
  },
  { path: '**', loadComponent: async () => (await import('@harmony/ui/shell')).NotFoundPage },
];
