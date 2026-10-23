import { Routes } from '@angular/router';
import { sessionRoutes } from '@harmony/platform/session';

export const routes: Routes = [
  ...sessionRoutes,
  { path: '', loadComponent: () => import('./home').then((m) => m.Home) },
  { path: 'hello', loadComponent: () => import('./hello').then((m) => m.Hello) },
];
