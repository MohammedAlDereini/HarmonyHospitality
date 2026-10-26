import { Routes } from '@angular/router';
import { GroupPage } from './group-page';
import { GroupsPage } from './groups-page';

/** Mounted at /groups behind allowed(Permission.ViewTenants) (architecture 10). */
export const routes: Routes = [
  {
    path: '',
    component: GroupsPage,
    title: $localize`:@@platform-admin.groups.page-title:Groups`,
  },
  {
    path: ':id',
    component: GroupPage,
    title: $localize`:@@platform-admin.group.page-title:Group`,
    data: { crumb: $localize`:@@platform-admin.groups.crumb:Groups` },
  },
];
