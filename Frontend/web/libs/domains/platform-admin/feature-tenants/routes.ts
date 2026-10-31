import { Routes } from '@angular/router';
import { NewTenantPage } from './new-tenant-page';
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
    path: 'new',
    component: NewTenantPage,
    title: $localize`:@@platform-admin.wizard.page-title:New tenant`,
    data: { crumb: $localize`:@@platform-admin.tenants.crumb:Tenants` },
  },
  {
    path: ':id',
    component: TenantPage,
    title: $localize`:@@platform-admin.tenant.page-title:Tenant`,
    data: { crumb: $localize`:@@platform-admin.tenants.crumb:Tenants` },
  },
];
