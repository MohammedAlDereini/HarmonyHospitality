import { Routes } from '@angular/router';
import { TenantPage } from './tenant-page';
import { TenantsPage } from './tenants-page';

/** Mounted at /tenants behind allowed(Permission.ViewTenants) (architecture 10). */
export const routes: Routes = [
  {
    path: '',
    component: TenantsPage,
    title: $localize`:@@platform-admin.tenants.page-title:Tenants`,
  },
  {
    path: ':id',
    component: TenantPage,
    title: $localize`:@@platform-admin.tenant.page-title:Tenant`,
    data: { crumb: $localize`:@@platform-admin.tenants.crumb:Tenants` },
  },
];
