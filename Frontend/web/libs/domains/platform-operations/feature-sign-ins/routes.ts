import { Routes } from '@angular/router';
import { SignInsPage } from './sign-ins-page';

/** Mounted at /sign-ins behind allowed(Permission.ViewTenants) (architecture 10). */
export const routes: Routes = [
  {
    path: '',
    component: SignInsPage,
    title: $localize`:@@platform-operations.sign-ins.page-title:Sign-ins and devices`,
  },
];
