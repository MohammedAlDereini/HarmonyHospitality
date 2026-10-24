import { Routes } from '@angular/router';
import { PropertiesPage } from './properties-page';
import { PropertyPage } from './property-page';

/** Mounted at /properties behind allowed(Permission.ViewTenants) (architecture 10). */
export const routes: Routes = [
  {
    path: '',
    component: PropertiesPage,
    title: $localize`:@@platform-admin.properties.page-title:Properties`,
  },
  {
    path: ':id',
    component: PropertyPage,
    title: $localize`:@@platform-admin.property.page-title:Property`,
    data: { crumb: $localize`:@@platform-admin.properties.crumb:Properties` },
  },
];
