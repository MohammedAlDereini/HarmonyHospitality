import { Routes } from '@angular/router';
import { ReferenceIndexPage } from './reference-index-page';
import { ReferenceTablePage } from './reference-table-page';

/** Mounted at /reference behind allowed(Permission.ViewTenants) (architecture 10). */
export const routes: Routes = [
  {
    path: '',
    component: ReferenceIndexPage,
    title: $localize`:@@platform-admin.reference.page-title:Reference data`,
  },
  {
    path: ':table',
    component: ReferenceTablePage,
    title: $localize`:@@platform-admin.reference.table.page-title:Table`,
    data: { crumb: $localize`:@@platform-admin.reference.crumb:Reference data` },
  },
];
