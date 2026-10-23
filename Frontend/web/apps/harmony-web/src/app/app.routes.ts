import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./home').then((m) => m.Home) },
  { path: 'hello', loadComponent: () => import('./hello').then((m) => m.Hello) },
];
