import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TENANTS_PAGE_SIZE, TenantsStore } from '@harmony/platform-admin/data-access';
import { Tenant, TenantStatus, tenantName } from '@harmony/platform-admin/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { Pager } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { Tone } from '@harmony/ui/primitives';
import { PageActionsTemplate } from '@harmony/ui/shell';
import { TenantEditor } from './tenant-editor';
import { statusLabel, statusToneOf } from './tenant-wording';

/** One choice of the status filter: the option's value, the status it stands for (null = all), the label. */
interface StatusChoice {
  readonly value: string;
  readonly status: TenantStatus | null;
  readonly label: string;
}

/**
 * The platform's customers the way the platform app listed things: a lead, the search and the status filter in a
 * toolbar, a real table, a pager; a new tenant in the drawer from the context bar's action slot (architecture 6,
 * platform-admin). The buttons are presentation: the API refuses on its own.
 */
@Component({
  selector: 'hr-tenants-page',
  imports: [RouterLink, IfAllowed, Icon, PageActionsTemplate, TenantEditor, Pager, Drawer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tenants-page.html',
  styleUrl: './tenants-page.css',
})
export class TenantsPage {
  protected readonly store = inject(TenantsStore);
  private readonly toasts = inject(Toasts);
  private readonly drawer = viewChild.required(Drawer);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly pageSize = TENANTS_PAGE_SIZE;
  protected readonly creating = signal(false);
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly statusValue = computed(() => this.store.filters().status ?? '');
  protected readonly choices: readonly StatusChoice[] = [
    { value: '', status: null, label: $localize`:@@platform-admin.tenants.filter.all:All` },
    {
      value: TenantStatus.active,
      status: TenantStatus.active,
      label: statusLabel({ status: TenantStatus.active }),
    },
    {
      value: TenantStatus.suspended,
      status: TenantStatus.suspended,
      label: statusLabel({ status: TenantStatus.suspended }),
    },
    {
      value: TenantStatus.terminated,
      status: TenantStatus.terminated,
      label: statusLabel({ status: TenantStatus.terminated }),
    },
  ];

  protected name(tenant: Tenant): string {
    return tenantName(tenant, this.locale);
  }

  protected status(tenant: Tenant): string {
    return statusLabel(tenant);
  }

  protected tone(tenant: Tenant): Tone | null {
    return statusToneOf(tenant);
  }

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.store.search(text);
  }

  protected onStatus(value: string): void {
    const choice = this.choices.find((candidate) => candidate.value === value);
    this.store.showStatus(choice?.status ?? null);
  }

  protected openCreator(): void {
    this.creating.set(true);
    this.drawer().open();
  }

  protected saved(): void {
    this.toasts.show($localize`:@@platform-admin.tenants.toast.added:Tenant added.`);
    this.drawer().close();
  }
}
