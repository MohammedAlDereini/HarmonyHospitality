import { Routes } from '@angular/router';
import { RolesConsolePage } from './roles-console-page';

/** The console's Roles (platform console design, C7), mounted at /roles behind allowed(Permission.ViewRoles). */
export const consoleRoutes: Routes = [
  {
    path: '',
    component: RolesConsolePage,
    title: $localize`:@@administration.roles.page-title:Roles`,
  },
];
