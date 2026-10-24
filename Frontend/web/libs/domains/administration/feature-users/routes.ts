import { Routes } from '@angular/router';
import { UsersPage } from './users-page';

/** Mounted at /p/:property/admin behind allowed(Permission.ViewUsers) (architecture 10). */
export const routes: Routes = [
  { path: '', component: UsersPage, title: $localize`:@@administration.users.page-title:Users` },
];
