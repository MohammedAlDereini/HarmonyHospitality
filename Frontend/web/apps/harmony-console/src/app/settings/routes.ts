import { Routes } from '@angular/router';
import { SettingsConsolePage } from './settings-console-page';

/** The console's Settings (platform console design, C11), mounted at /settings behind allowed(Permission.ViewSettings). */
export const consoleRoutes: Routes = [
  {
    path: '',
    component: SettingsConsolePage,
    title: $localize`:@@administration.settings.page-title:Settings`,
  },
];
