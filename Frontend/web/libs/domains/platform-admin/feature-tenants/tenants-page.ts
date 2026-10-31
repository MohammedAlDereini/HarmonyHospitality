import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  afterNextRender,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  ReferenceStore,
  TENANTS_PAGE_SIZE,
  TenantsStore,
} from '@harmony/platform-admin/data-access';
import {
  ReferenceRow,
  TENANT_MAX_TIER,
  TENANT_MIN_TIER,
  Tenant,
  TenantStatus,
  tenantName,
} from '@harmony/platform-admin/domain';
import { SummariesStore } from '@harmony/platform-operations/data-access';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { csvFile, initialsOf } from '@harmony/shared/domain';
import { Facet, FacetOption, Pager, saveTextFile } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead, PopAt } from '@harmony/ui/shell';
import { ReasonForm } from './reason-form';
import { TenantEditor } from './tenant-editor';
import { statusLabel } from './tenant-wording';

/** What the drawer holds. */
type Opened = 'edit' | 'suspend';

/** The chip's tone beside the status word (status never by colour alone). */
type ChipTone = 'ok' | 'warn' | 'stop' | null;

const DAY_MS = 86_400_000;
const dateFormat: Intl.DateTimeFormatOptions = {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
};
const knownStatuses: readonly TenantStatus[] = [
  TenantStatus.active,
  TenantStatus.suspended,
  TenantStatus.terminated,
];

/** A reference row's text under a key; '' when the row has none. */
const textOf = (row: ReferenceRow, key: string): string => {
  const value = row[key];
  return typeof value === 'string' ? value : '';
};

/**
 * The platform's customers (platform console design, C2): the lead counts them, four facets narrow the list, each row
 * says who the customer is in both languages, whether they can sign in, what they run and who administers them; the
 * row's menu offers exactly the doors TenantController has. Reads through the store; every write carries its own
 * intent key and reads the list again.
 */
@Component({
  selector: 'hr-tenants-page',
  imports: [
    RouterLink,
    IfAllowed,
    Icon,
    Facet,
    Pager,
    Drawer,
    PageHead,
    PopAt,
    TenantEditor,
    ReasonForm,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tenants-page.html',
  styleUrl: './tenants-page.css',
})
export class TenantsPage {
  protected readonly store = inject(TenantsStore);
  private readonly reference = inject(ReferenceStore);
  private readonly summaries = inject(SummariesStore);
  private readonly toasts = inject(Toasts);
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);
  private readonly drawer = viewChild.required(Drawer);
  private readonly locale = $localize.locale ?? 'en';
  /** Day, month, year in English ("12 Mar 2026"); the locale's own order otherwise. */
  private readonly dates = new Intl.DateTimeFormat(
    this.locale === 'en' ? 'en-GB' : this.locale,
    dateFormat,
  );
  private readonly countries = this.reference.choices('Country');
  private readonly modules = this.reference.choices('Module');

  protected readonly manageTenants = Permission.ManageTenants;
  /** ?create=1: the console's home and the rail's Onboarding send a person straight to a new tenant. */
  readonly create = input<string>();
  protected readonly pageSize = TENANTS_PAGE_SIZE;
  protected readonly opened = signal<Opened | null>(null);
  /** The row an action is about (edit, suspend). */
  protected readonly chosen = signal<Tenant | null>(null);
  protected readonly busy = signal(false);
  protected readonly actionProblem = signal<string | null>(null);
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly summary = computed(() => this.summaries.tenants());

  protected readonly statusOptions: readonly FacetOption[] = knownStatuses.map((status) => ({
    value: status,
    label: statusLabel({ status }),
  }));
  protected readonly tierOptions: readonly FacetOption[] = Array.from(
    { length: TENANT_MAX_TIER - TENANT_MIN_TIER + 1 },
    (_, index) => String(TENANT_MIN_TIER + index),
  ).map((tier) => ({ value: tier, label: tier }));
  protected readonly countryOptions = computed<readonly FacetOption[]>(() =>
    this.countries.rows().map((row) => ({
      value: textOf(row, 'code'),
      label: `${textOf(row, 'code')} · ${this.referenceName(row)}`,
    })),
  );
  protected readonly moduleOptions = computed<readonly FacetOption[]>(() =>
    this.modules.rows().map((row) => ({
      value: textOf(row, 'code'),
      label: `${textOf(row, 'code')} · ${this.referenceName(row)}`,
    })),
  );
  protected readonly statusValues = computed<readonly string[]>(
    () => this.store.filters().statuses,
  );
  protected readonly tierValue = computed<readonly string[]>(() => {
    const tier = this.store.filters().tier;
    return tier === null ? [] : [String(tier)];
  });
  protected readonly countryValue = computed<readonly string[]>(() => {
    const country = this.store.filters().countryCode;
    return country === null ? [] : [country];
  });
  protected readonly moduleValue = computed<readonly string[]>(() => {
    const module = this.store.filters().moduleCode;
    return module === null ? [] : [module];
  });

  protected readonly drawerHeading = computed(() => {
    const opened = this.opened();
    if (opened === 'suspend')
      return $localize`:@@platform-admin.tenants.suspend.title:Suspend tenant`;
    return $localize`:@@platform-admin.tenants.edit.title:Edit tenant`;
  });
  protected readonly suspendLabel = $localize`:@@platform-admin.tenants.suspend.label:Reason`;
  protected readonly suspendPlaceholder = $localize`:@@platform-admin.tenants.suspend.placeholder:Why the customer can no longer sign in`;
  protected readonly suspendGo = $localize`:@@platform-admin.tenants.suspend.go:Suspend`;

  constructor() {
    this.countries.load();
    this.modules.load();
    afterNextRender(() => {
      if (this.create() === '1') void this.router.navigateByUrl('/tenants/new');
    });
  }

  protected name(tenant: Tenant): string {
    return tenantName(tenant, this.locale);
  }

  /** Active with an administrator still to accept the invitation reads Onboarding (C2). */
  protected isOnboarding(tenant: Tenant): boolean {
    const administrator = tenant.administrator;
    return (
      tenant.status === TenantStatus.active &&
      administrator !== null &&
      (administrator.invitationPending || administrator.invitationExpired)
    );
  }

  protected statusWord(tenant: Tenant): string {
    return this.isOnboarding(tenant)
      ? $localize`:@@platform-admin.tenants.status.onboarding:Onboarding`
      : statusLabel(tenant);
  }

  protected chipTone(tenant: Tenant): ChipTone {
    if (this.isOnboarding(tenant)) return 'warn';
    if (tenant.status === TenantStatus.active) return 'ok';
    if (tenant.status === TenantStatus.suspended) return 'stop';
    return null;
  }

  /** "740 · 712": rooms licensed today, then rooms the hotels declare. */
  protected rooms(tenant: Tenant): string {
    return `${String(tenant.licensedRooms)} · ${String(tenant.roomsInUse)}`;
  }

  /** Rooms above the licence, 0 when the licence covers them or no line was ever bought. */
  protected over(tenant: Tenant): number {
    if (tenant.licensedRooms === 0 || tenant.roomsInUse <= tenant.licensedRooms) return 0;
    return tenant.roomsInUse - tenant.licensedRooms;
  }

  protected moduleList(tenant: Tenant): string {
    return tenant.moduleCodes.length === 0 ? '—' : tenant.moduleCodes.join(' · ');
  }

  protected onboarded(tenant: Tenant): string {
    return this.dates.format(new Date(`${tenant.onboardedOn}T00:00:00Z`));
  }

  protected administratorName(tenant: Tenant): string {
    return (
      tenant.administrator?.displayName ??
      $localize`:@@platform-admin.tenants.administrator.none:no administrator`
    );
  }

  /** Under the administrator's name: the e-mail, or where the invitation stands. */
  protected administratorNote(tenant: Tenant): string {
    const administrator = tenant.administrator;
    if (administrator === null) return tenant.statusReason ?? '';
    const days = this.daysSince(administrator.invitedOn);
    if (administrator.invitationPending)
      return $localize`:@@platform-admin.tenants.administrator.pending:invitation not accepted · ${days}:days: d`;
    if (administrator.invitationExpired)
      return $localize`:@@platform-admin.tenants.administrator.expired:invitation expired · ${days}:days: d`;
    return administrator.email ?? '';
  }

  protected initials(tenant: Tenant): string {
    return tenant.administrator === null ? '—' : initialsOf(tenant.administrator.displayName);
  }

  protected isActive(tenant: Tenant): boolean {
    return tenant.status === TenantStatus.active;
  }

  protected isSuspended(tenant: Tenant): boolean {
    return tenant.status === TenantStatus.suspended;
  }

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.store.search(text);
  }

  protected onStatuses(values: readonly string[]): void {
    this.store.showStatuses(knownStatuses.filter((status) => values.includes(status)));
  }

  protected onTier(values: readonly string[]): void {
    const first = values[0];
    this.store.showTier(first === undefined ? null : Number(first));
  }

  protected onCountry(values: readonly string[]): void {
    this.store.showCountry(values[0] ?? null);
  }

  protected onModule(values: readonly string[]): void {
    this.store.showModule(values[0] ?? null);
  }

  protected edit(tenant: Tenant): void {
    this.open('edit', tenant);
  }

  protected askSuspend(tenant: Tenant): void {
    this.open('suspend', tenant);
  }

  protected suspend(tenant: Tenant, reason: string): void {
    void this.press(this.store.suspend(tenant, reason, newIntentKey()), () => {
      this.toasts.show($localize`:@@platform-admin.tenants.toast.suspended:Tenant suspended.`);
      this.drawer().close();
    });
  }

  protected reinstate(tenant: Tenant): void {
    void this.press(this.store.reinstate(tenant, newIntentKey()), () => {
      this.toasts.show($localize`:@@platform-admin.tenants.toast.reinstated:Tenant reinstated.`);
    });
  }

  /** The drawer has closed, whatever closed it. */
  protected drawerClosed(): void {
    this.opened.set(null);
    this.chosen.set(null);
  }

  protected saved(): void {
    this.toasts.show(
      this.opened() === 'edit'
        ? $localize`:@@platform-admin.tenants.toast.saved:Tenant saved.`
        : $localize`:@@platform-admin.tenants.toast.added:Tenant added.`,
    );
    this.drawer().close();
  }

  /** The rows on screen as a CSV file the browser saves; nothing leaves the page but to the person's disk. */
  protected exportCsv(): void {
    const rows = this.store.rows();
    if (rows.length === 0) return;
    const header = [
      'Code',
      'Name',
      'Name (Arabic)',
      'Status',
      'Tier',
      'Country',
      'Currency',
      'Hotels',
      'Rooms licensed',
      'Rooms in use',
      'Modules',
      'Onboarded',
      'Administrator',
      'Administrator e-mail',
    ];
    const lines = rows.map((tenant) => [
      tenant.code,
      tenant.legalNameEn,
      tenant.legalNameAr,
      this.statusWord(tenant),
      tenant.tier,
      tenant.countryCode,
      tenant.defaultCurrency,
      tenant.hotels,
      tenant.licensedRooms,
      tenant.roomsInUse,
      tenant.moduleCodes.join(' '),
      tenant.onboardedOn,
      tenant.administrator?.displayName ?? '',
      tenant.administrator?.email ?? '',
    ]);
    saveTextFile(
      this.document,
      'harmony-tenants.csv',
      csvFile(header, lines),
      'text/csv;charset=utf-8',
    );
    this.toasts.show(
      $localize`:@@platform-admin.tenants.exported:${rows.length}:count: tenants exported.`,
    );
  }

  private open(what: Opened, tenant: Tenant | null): void {
    this.actionProblem.set(null);
    this.chosen.set(tenant);
    this.opened.set(what);
    this.drawer().open();
  }

  private referenceName(row: ReferenceRow): string {
    const arabic = textOf(row, 'nameAr');
    return this.locale.startsWith('ar') && arabic !== '' ? arabic : textOf(row, 'nameEn');
  }

  private daysSince(instant: string | null): number {
    if (instant === null) return 0;
    const elapsed = Date.now() - new Date(instant).getTime();
    return Math.max(0, Math.floor(elapsed / DAY_MS));
  }

  /** One press: busy while Identity answers; a refusal is worded on the page, success runs `done`. */
  private async press(call: Promise<ApiResult<unknown>>, done: () => void): Promise<void> {
    this.busy.set(true);
    this.actionProblem.set(null);
    const result = await call;
    this.busy.set(false);
    if (result.kind === 'ok') {
      done();
      return;
    }
    this.actionProblem.set(messageOf(result));
    this.toasts.show(messageOf(result), true);
  }
}
