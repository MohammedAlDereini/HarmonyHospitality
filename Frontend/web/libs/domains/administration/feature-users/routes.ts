import { Routes } from '@angular/router';
import { CompanyLoginPage } from './company-login-page';
import { PersonPage } from './person-page';
import { UsersPage } from './users-page';

/** Mounted at /p/:property/admin behind allowed(Permission.ViewUsers) (architecture 10). */
export const routes: Routes = [
  { path: '', component: UsersPage, title: $localize`:@@administration.users.page-title:Users` },
  {
    path: 'company-login',
    component: CompanyLoginPage,
    title: $localize`:@@administration.company-login.page-title:Company login`,
    data: { crumb: $localize`:@@administration.users.crumb:Users` },
  },
  {
    path: 'account/:id',
    component: PersonPage,
    title: $localize`:@@administration.person.page-title:Person`,
    data: { crumb: $localize`:@@administration.users.crumb:Users` },
  },
];
