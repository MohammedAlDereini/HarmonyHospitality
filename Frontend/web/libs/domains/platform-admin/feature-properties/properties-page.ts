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
import {
  PROPERTIES_PAGE_SIZE,
  PropertiesStore,
  ReferenceStore,
} from '@harmony/platform-admin/data-access';
import { Property, PropertyState, ReferenceRow } from '@harmony/platform-admin/domain';
import { SummariesStore, TenantChoicesStore } from '@harmony/platform-operations/data-access';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { csvFile } from '@harmony/shared/domain';
import { Facet, FacetOption, Pager, saveTextFile } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead, PopAt } from '@harmony/ui/shell';
import { PropertyEditor } from './property-editor';

/** The chip's tone beside the state word (never colour alone). */
type ChipTone = 'ok' | 'info' | null;

const dateFormat: Intl.DateTimeFormatOptions = {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
};
const states: readonly PropertyState[] = ['Active', 'Opening', 'Closed'];
const isState = (value: string): value is PropertyState =>
  (states as readonly string[]).includes(value);

/** A reference row's text under a key; '' when the row has none. */
const textOf = (row: ReferenceRow, key: string): string => {
  const value = row[key];
  return typeof value === 'string' ? value : '';
};

/**
 * Every customer's hotels (platform console design, C4), or one tenant's when the URL says ?tenant=: the lead
 * counts them, four facets and the licence toggle narrow the list, each row says the hotel in both languages, its
 * customer, where it is, what it runs and how its rooms stand against the licence. Reads through the store; a new
 * hotel is made in the drawer with the press's own intent key.
 */
@Component({
  selector: 'hr-properties-page',
  imports: [RouterLink, IfAllowed, Icon, Facet, Pager, Drawer, PageHead, PopAt, PropertyEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './properties-page.html',
  styleUrl: './properties-page.css',
})
export class PropertiesPage {
  /** From the query string (?tenant=), bound by the router. */
  readonly tenant = input<string | undefined>();

  protected readonly store = inject(PropertiesStore);
  private readonly reference = inject(ReferenceStore);
  private readonly summaries = inject(SummariesStore);
  protected readonly tenants = inject(TenantChoicesStore);
  private readonly toasts = inject(Toasts);
  private readonly document = inject(DOCUMENT);
  private readonly drawer = viewChild.required(Drawer);
  private readonly locale = $localize.locale ?? 'en';
  private readonly dates = new Intl.DateTimeFormat(
    this.locale === 'en' ? 'en-GB' : this.locale,
    dateFormat,
  );
  private readonly countries = this.reference.choices('Country');
  private readonly types = this.reference.choices('PropertyType');
  private readonly cities = this.reference.choices('City');

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly pageSize = PROPERTIES_PAGE_SIZE;
  protected readonly creating = signal(false);
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly tenantId = computed(() => this.tenant() ?? null);
  protected readonly summary = computed(() => this.summaries.properties());

  protected readonly stateOptions: readonly FacetOption[] = [
    { value: 'Active', label: $localize`:@@platform-admin.properties.state.active:Active` },
    { value: 'Opening', label: $localize`:@@platform-admin.properties.state.opening:Opening` },
    { value: 'Closed', label: $localize`:@@platform-admin.properties.state.closed:Closed` },
  ];
  protected readonly tenantOptions = computed<readonly FacetOption[]>(() =>
    this.tenants.rows().map((row) => ({ value: row.id, label: row.code })),
  );
  protected readonly countryOptions = computed<readonly FacetOption[]>(() =>
    this.countries.rows().map((row) => ({
      value: textOf(row, 'code'),
      label: `${textOf(row, 'code')} · ${this.referenceName(row)}`,
    })),
  );
  protected readonly typeOptions = computed<readonly FacetOption[]>(() =>
    this.types
      .rows()
      .map((row) => ({ value: textOf(row, 'code'), label: this.referenceName(row) })),
  );
  protected readonly tenantValue = computed<readonly string[]>(() => {
    const id = this.store.filters().tenantId;
    return id === null ? [] : [id];
  });
  protected readonly countryValue = computed<readonly string[]>(() => {
    const code = this.store.filters().countryCode;
    return code === null ? [] : [code];
  });
  protected readonly stateValue = computed<readonly string[]>(() => {
    const state = this.store.filters().state;
    return state === null ? [] : [state];
  });
  protected readonly typeValue = computed<readonly string[]>(() => {
    const code = this.store.filters().typeCode;
    return code === null ? [] : [code];
  });
  protected readonly aboveLicence = computed(() => this.store.filters().aboveLicence);
  /** The person narrowed the list: the search, a facet or the licence switch is set beyond the tenant the URL names. */
  protected readonly narrowed = computed(() => {
    const filters = this.store.filters();
    return (
      filters.search !== '' ||
      filters.countryCode !== null ||
      filters.state !== null ||
      filters.typeCode !== null ||
      filters.aboveLicence ||
      filters.tenantId !== (this.tenant() ?? null)
    );
  });

  constructor() {
    this.countries.load();
    this.types.load();
    this.cities.load();
    effect(() => {
      const tenantId = this.tenantId();
      untracked(() => {
        this.store.ofTenant(tenantId);
      });
    });
  }

  protected typeName(property: Property): string {
    const row = this.types
      .rows()
      .find((candidate) => textOf(candidate, 'code') === property.propertyTypeCode);
    return row === undefined ? property.propertyTypeCode : this.referenceName(row);
  }

  /** "Amman · Asia/Amman"; the zone alone until the cities are read. */
  protected where(property: Property): string {
    const row = this.cities.rows().find((candidate) => textOf(candidate, 'id') === property.cityId);
    const city = row === undefined ? '' : this.referenceName(row);
    return city === '' ? property.timeZoneId : `${city} · ${property.timeZoneId}`;
  }

  /** "132 · 130": licensed today, then declared. */
  protected rooms(property: Property): string {
    const licensed = property.licensedRooms === null ? '—' : String(property.licensedRooms);
    return `${licensed} · ${String(property.totalRooms)}`;
  }

  protected over(property: Property): number {
    if (property.licensedRooms === null || property.totalRooms <= property.licensedRooms) return 0;
    return property.totalRooms - property.licensedRooms;
  }

  protected modules(property: Property): string {
    return property.moduleCodes.length === 0 ? '—' : property.moduleCodes.join(' · ');
  }

  /** "15 Dec 2026 · —": when it opens, when it closes. */
  protected span(property: Property): string {
    return `${this.date(property.opensOn)} · ${this.date(property.closesOn)}`;
  }

  protected stateWord(property: Property): string {
    if (!property.isActive || this.closed(property))
      return $localize`:@@platform-admin.properties.state.closed:Closed`;
    if (this.opensLater(property))
      return $localize`:@@platform-admin.properties.state.opening:Opening`;
    return $localize`:@@platform-admin.properties.state.active:Active`;
  }

  protected stateTone(property: Property): ChipTone {
    if (!property.isActive || this.closed(property)) return null;
    return this.opensLater(property) ? 'info' : 'ok';
  }

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.store.search(text);
  }

  protected onTenant(values: readonly string[]): void {
    this.store.ofTenant(values[0] ?? null);
  }

  protected onCountry(values: readonly string[]): void {
    this.store.showCountry(values[0] ?? null);
  }

  protected onState(values: readonly string[]): void {
    const first = values[0];
    this.store.showState(first !== undefined && isState(first) ? first : null);
  }

  protected onType(values: readonly string[]): void {
    this.store.showType(values[0] ?? null);
  }

  protected onAboveLicence(checked: boolean): void {
    this.store.onlyAboveLicence(checked);
  }

  protected openCreator(): void {
    this.creating.set(true);
    this.drawer().open();
  }

  protected saved(): void {
    this.summaries.reload();
    this.toasts.show($localize`:@@platform-admin.properties.toast.added:Property added.`);
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
      'Tenant',
      'Country',
      'Time zone',
      'Type',
      'Rooms licensed',
      'Rooms declared',
      'Modules',
      'Opens',
      'Closes',
      'State',
    ];
    const lines = rows.map((property) => [
      property.code,
      property.nameEn,
      property.nameAr,
      property.tenantCode,
      property.countryCode,
      property.timeZoneId,
      property.propertyTypeCode,
      property.licensedRooms,
      property.totalRooms,
      property.moduleCodes.join(' '),
      property.opensOn,
      property.closesOn,
      this.stateWord(property),
    ]);
    saveTextFile(
      this.document,
      'harmony-properties.csv',
      csvFile(header, lines),
      'text/csv;charset=utf-8',
    );
    this.toasts.show(
      $localize`:@@platform-admin.properties.exported:${rows.length}:count: properties exported.`,
    );
  }

  private date(day: string | null): string {
    return day === null ? '—' : this.dates.format(new Date(`${day}T00:00:00Z`));
  }

  private opensLater(property: Property): boolean {
    return (
      property.opensOn !== null && new Date(`${property.opensOn}T00:00:00Z`).getTime() > Date.now()
    );
  }

  private closed(property: Property): boolean {
    return (
      property.closesOn !== null &&
      new Date(`${property.closesOn}T00:00:00Z`).getTime() < Date.now()
    );
  }

  private referenceName(row: ReferenceRow): string {
    const arabic = textOf(row, 'nameAr');
    return this.locale.startsWith('ar') && arabic !== '' ? arabic : textOf(row, 'nameEn');
  }
}
