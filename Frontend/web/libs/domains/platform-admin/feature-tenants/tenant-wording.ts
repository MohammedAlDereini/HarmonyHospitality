import { Tenant, TenantStatus } from '@harmony/platform-admin/domain';
import { Tone } from '@harmony/ui/primitives';

const statusWording: Readonly<Record<TenantStatus, string>> = {
  [TenantStatus.active]: $localize`:@@platform-admin.tenants.status.active:Active`,
  [TenantStatus.suspended]: $localize`:@@platform-admin.tenants.status.suspended:Suspended`,
  [TenantStatus.terminated]: $localize`:@@platform-admin.tenants.status.terminated:Terminated`,
};

/** Text says the status; the tone only underlines it (status never by colour alone). */
const statusTone: Readonly<Record<TenantStatus, Tone | null>> = {
  [TenantStatus.active]: null,
  [TenantStatus.suspended]: 'warn',
  [TenantStatus.terminated]: 'stop',
};

export const statusLabel = (tenant: Pick<Tenant, 'status'>): string => statusWording[tenant.status];

export const statusToneOf = (tenant: Pick<Tenant, 'status'>): Tone | null =>
  statusTone[tenant.status];
