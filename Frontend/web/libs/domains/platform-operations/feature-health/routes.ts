import { Routes } from '@angular/router';
import { HealthPage } from './health-page';

/** Mounted at /health behind allowed(Permission.ViewSettings) (architecture 10). */
export const routes: Routes = [
  {
    path: '',
    component: HealthPage,
    title: $localize`:@@platform-operations.health.page-title:Platform health`,
  },
];
