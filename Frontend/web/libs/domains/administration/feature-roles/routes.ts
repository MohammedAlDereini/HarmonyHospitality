import { Routes } from '@angular/router';
import { RolesPage } from './roles-page';

/** Mounted at /p/:property/admin/roles behind allowed(Permission.ViewRoles) (architecture 10). */
export const routes: Routes = [
  { path: '', component: RolesPage, title: $localize`:@@administration.roles.page-title:Roles` },
];
