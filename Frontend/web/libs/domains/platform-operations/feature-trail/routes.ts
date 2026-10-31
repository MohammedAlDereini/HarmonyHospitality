import { Routes } from '@angular/router';
import { TrailPage } from './trail-page';

/** Mounted at /trail behind allowed(Permission.ViewTenants) (architecture 10). */
export const routes: Routes = [
  {
    path: '',
    component: TrailPage,
    title: $localize`:@@platform-operations.trail.page-title:Audit log`,
  },
];
