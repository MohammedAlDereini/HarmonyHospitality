import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { GROUPS_PAGE_SIZE, GroupsStore, ReferenceStore } from '@harmony/platform-admin/data-access';
import {
  BrandDetail,
  PropertyGroup,
  ReferenceRow,
  groupTitle,
  isBrandType,
} from '@harmony/platform-admin/domain';
import { TenantChoicesStore } from '@harmony/platform-operations/data-access';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { csvFile } from '@harmony/shared/domain';
import { Facet, FacetOption, Pager, saveTextFile } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead, PopAt } from '@harmony/ui/shell';
import { GroupEditor } from './group-editor';

/** The State facet's two choices. */
const ACTIVE = 'active';
const INACTIVE = 'inactive';

/** A reference row's text under a key; '' when the row has none. */
const textOf = (row: ReferenceRow, key: string): string => {
  const value = row[key];
  return typeof value === 'string' ? value : '';
};

/** "01 Jan 2024 – 31 Dec 2028" (each day through `day`), or '—' when the brand has no agreement dates. */
export const agreementOf = (
  brand: BrandDetail | null,
  day: (iso: string) => string = (iso) => iso,
): string => {
  if (brand === null || (brand.agreementStartsOn === null && brand.agreementEndsOn === null))
    return '—';
  const starts = brand.agreementStartsOn === null ? '…' : day(brand.agreementStartsOn);
  const ends = brand.agreementEndsOn === null ? '…' : day(brand.agreementEndsOn);
  return `${starts} – ${ends}`;
};

/**
 * Every customer's hotel groups (platform console design, C14), or one tenant's when the URL says ?tenant=: the lead
 * counts them by type, three facets narrow the list, each row says the group, whose it is, its type, its hotels, its
 * precedence, the brand's agreement and its state; the row's menu offers the doors the console has. A new group is
 * made in the drawer.
 */
@Component({
  selector: 'hr-groups-page',
  imports: [RouterLink, IfAllowed, Icon, Facet, Pager, Drawer, PageHead, PopAt, GroupEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './groups-page.html',
  styleUrl: './groups-page.css',
})
export class GroupsPage {
  /** From the query string (?tenant=), bound by the router. */
  readonly tenant = input<string | undefined>();

  protected readonly store = inject(GroupsStore);
  private readonly reference = inject(ReferenceStore);
  private readonly tenants = inject(TenantChoicesStore);
  private readonly toasts = inject(Toasts);
  private readonly document = inject(DOCUMENT);
  private readonly drawer = viewChild.required(Drawer);
  private readonly locale = $localize.locale ?? 'en';
  private readonly types = this.reference.choices('PropertyGroupType');

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly pageSize = GROUPS_PAGE_SIZE;
  protected readonly creating = signal(false);
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly tenantId = computed(() => this.tenant() ?? null);

  protected readonly tenantOptions = computed<readonly FacetOption[]>(() =>
    this.tenants
      .rows()
      .filter((row) => !row.isPlatform)
      .map((row) => ({ value: row.id, label: `${row.code} · ${this.tenantName(row.id)}` })),
  );
  protected readonly typeOptions = computed<readonly FacetOption[]>(() =>
    this.types
      .rows()
      .map((row) => ({ value: textOf(row, 'code'), label: this.referenceName(row) })),
  );
  protected readonly stateOptions: readonly FacetOption[] = [
    { value: ACTIVE, label: $localize`:@@platform-admin.groups.active:Active` },
    { value: INACTIVE, label: $localize`:@@platform-admin.groups.inactive:Inactive` },
  ];
  protected readonly tenantValue = computed<readonly string[]>(() => {
    const tenantId = this.store.filters().tenantId;
    return tenantId === null ? [] : [tenantId];
  });
  protected readonly typeValue = computed<readonly string[]>(() => {
    const typeCode = this.store.filters().typeCode;
    return typeCode === null ? [] : [typeCode];
  });
  protected readonly stateValue = computed<readonly string[]>(() => {
    const isActive = this.store.filters().isActive;
    if (isActive === null) return [];
    return [isActive ? ACTIVE : INACTIVE];
  });
  /** The whole list fits one page: its counts by type can be said. */
  protected readonly complete = computed(
    () => this.store.rows().length === this.store.total() && this.store.total() > 0,
  );
  /** "2 Brand · 2 Region · 1 Owner", in the reference table's order, from the rows on hand. */
  protected readonly byType = computed(() => {
    const rows = this.store.rows();
    return this.types
      .rows()
      .map((row) => ({
        name: this.referenceName(row),
        count: rows.filter((group) => group.typeCode === textOf(row, 'code')).length,
      }))
      .filter((part) => part.count > 0)
      .map((part) => `${String(part.count)} ${part.name}`)
      .join(' · ');
  });
  protected readonly inactive = computed(
    () => this.store.rows().filter((group) => !group.isActive).length,
  );
  /** The person narrowed the list: the search or a facet is set beyond the tenant the URL names. */
  protected readonly narrowed = computed(() => {
    const filters = this.store.filters();
    return (
      filters.search !== '' ||
      filters.typeCode !== null ||
      filters.isActive !== null ||
      filters.tenantId !== (this.tenant() ?? null)
    );
  });

  constructor() {
    this.types.load();
    effect(() => {
      const tenantId = this.tenantId();
      untracked(() => {
        this.store.ofTenant(tenantId);
      });
    });
  }

  protected name(group: PropertyGroup): string {
    return groupTitle(group, this.locale);
  }

  /** The second line: the external id, then the franchisor (a brand) or the description. */
  protected sub(group: PropertyGroup): string {
    const franchisor = group.brand?.franchisorName ?? null;
    const about =
      franchisor === null
        ? (group.description ?? '')
        : $localize`:@@platform-admin.groups.franchisor:franchisor ${franchisor}:name:`;
    return [group.externalIdentifier ?? '', about].filter((part) => part !== '').join(' · ');
  }

  protected tenantCode(group: PropertyGroup): string {
    return this.tenants.rows().find((row) => row.id === group.tenantId)?.code ?? '';
  }

  protected typeName(group: PropertyGroup): string {
    const row = this.types.rows().find((candidate) => textOf(candidate, 'code') === group.typeCode);
    return row === undefined ? group.typeCode : this.referenceName(row);
  }

  protected isBrand(group: PropertyGroup): boolean {
    return isBrandType(group.typeCode);
  }

  /** Dates the way the rest of the console writes them, in the person's language. */
  private readonly shortDates = new Intl.DateTimeFormat(
    this.locale === 'en' ? 'en-GB' : this.locale,
    {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      timeZone: 'UTC',
    },
  );

  protected agreement(group: PropertyGroup): string {
    return agreementOf(group.brand, (iso) => this.shortDates.format(new Date(`${iso}T00:00:00Z`)));
  }

  protected stateWord(group: PropertyGroup): string {
    return group.isActive
      ? $localize`:@@platform-admin.groups.active:Active`
      : $localize`:@@platform-admin.groups.inactive:Inactive`;
  }

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.store.search(text);
  }

  protected onTenant(values: readonly string[]): void {
    this.store.ofTenant(values[0] ?? null);
  }

  protected onType(values: readonly string[]): void {
    this.store.ofType(values[0] ?? null);
  }

  protected onState(values: readonly string[]): void {
    const chosen = values[0];
    this.store.showState(chosen === undefined ? null : chosen === ACTIVE);
  }

  protected openCreator(): void {
    this.creating.set(true);
    this.drawer().open();
  }

  protected saved(): void {
    this.toasts.show($localize`:@@platform-admin.groups.toast.added:Group added.`);
    this.drawer().close();
  }

  protected exportCsv(): void {
    const rows = this.store.rows();
    if (rows.length === 0) return;
    const header = [
      'Code',
      'Name',
      'Name (Arabic)',
      'Tenant',
      'Type',
      'Hotels',
      'Precedence',
      'Agreement starts',
      'Agreement ends',
      'State',
      'External identifier',
    ];
    const lines = rows.map((group) => [
      group.code,
      group.nameEn,
      group.nameAr,
      this.tenantCode(group),
      group.typeCode,
      group.currentMembers,
      group.precedence,
      group.brand?.agreementStartsOn ?? '',
      group.brand?.agreementEndsOn ?? '',
      this.stateWord(group),
      group.externalIdentifier ?? '',
    ]);
    saveTextFile(
      this.document,
      'harmony-groups.csv',
      csvFile(header, lines),
      'text/csv;charset=utf-8',
    );
    this.toasts.show(
      $localize`:@@platform-admin.groups.exported:${rows.length}:count: groups exported.`,
    );
  }

  private tenantName(id: string): string {
    const row = this.tenants.rows().find((candidate) => candidate.id === id);
    if (row === undefined) return '';
    return this.locale.startsWith('ar') && row.legalNameAr !== ''
      ? row.legalNameAr
      : row.legalNameEn;
  }

  private referenceName(row: ReferenceRow): string {
    const arabic = textOf(row, 'nameAr');
    return this.locale.startsWith('ar') && arabic !== '' ? arabic : textOf(row, 'nameEn');
  }
}
