import { Routes } from '@angular/router';
import { SettingsPage } from './settings-page';

/** Mounted at /p/:property/admin/settings behind allowed(Permission.ViewSettings) (architecture 10). */
export const routes: Routes = [
  {
    path: '',
    component: SettingsPage,
    title: $localize`:@@administration.settings.page-title:Settings`,
  },
];
