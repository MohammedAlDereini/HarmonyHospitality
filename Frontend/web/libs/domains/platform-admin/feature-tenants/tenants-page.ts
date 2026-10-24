import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TENANTS_PAGE_SIZE, TenantsStore } from '@harmony/platform-admin/data-access';
import { Tenant, TenantStatus, tenantName } from '@harmony/platform-admin/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { Icon } from '@harmony/ui/icons';
import { Tone } from '@harmony/ui/primitives';
import { PageActionsTemplate } from '@harmony/ui/shell';
import { TenantEditor } from './tenant-editor';
import { statusLabel, statusToneOf } from './tenant-wording';

const PAGE_STEP = 1;

/** One choice of the status filter. */
interface StatusChoice {
  readonly status: TenantStatus | null;
  readonly label: string;
}

/**
 * The platform's customers: code, names, country and currency, tier, status; a new tenant from the context bar's
 * action slot (architecture 6, platform-admin). The buttons are presentation: the API refuses on its own.
 */
@Component({
  selector: 'hr-tenants-page',
  imports: [RouterLink, IfAllowed, Icon, PageActionsTemplate, TenantEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tenants-page.html',
  styleUrl: './tenants-page.css',
})
export class TenantsPage {
  protected readonly store = inject(TenantsStore);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly creating = signal(false);
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly from = computed(
    () => (this.page() - PAGE_STEP) * TENANTS_PAGE_SIZE + PAGE_STEP,
  );
  protected readonly to = computed(() =>
    Math.min(this.page() * TENANTS_PAGE_SIZE, this.store.total()),
  );
  protected readonly hasPrevious = computed(() => this.page() > PAGE_STEP);
  protected readonly hasNext = computed(() => this.to() < this.store.total());
  protected readonly choices: readonly StatusChoice[] = [
    { status: null, label: $localize`:@@platform-admin.tenants.filter.all:All` },
    { status: TenantStatus.active, label: statusLabel({ status: TenantStatus.active }) },
    { status: TenantStatus.suspended, label: statusLabel({ status: TenantStatus.suspended }) },
    { status: TenantStatus.terminated, label: statusLabel({ status: TenantStatus.terminated }) },
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

  protected previous(): void {
    this.store.goTo(this.page() - PAGE_STEP);
  }

  protected next(): void {
    this.store.goTo(this.page() + PAGE_STEP);
  }

  protected openCreator(): void {
    this.creating.set(true);
  }

  protected closeCreator(): void {
    this.creating.set(false);
  }
}
