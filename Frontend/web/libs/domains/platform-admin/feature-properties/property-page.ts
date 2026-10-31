import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormField, form, pattern, required, submit } from '@angular/forms/signals';
import { GroupsStore, PropertiesStore, ReferenceStore } from '@harmony/platform-admin/data-access';
import {
  Property,
  PropertyGroup,
  PropertyUpdate,
  ReferenceRow,
  SubscriptionLine,
} from '@harmony/platform-admin/domain';
import { TrailCard } from '@harmony/platform-admin/ui';
import { TenantChoicesStore, TrailStore } from '@harmony/platform-operations/data-access';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead, PopAt } from '@harmony/ui/shell';

// A whole number of rooms, 1 or more. Matches "120"; rejects "0" and "12a".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5B1-9%5D%5B0-9%5D*%5Cs*%24&testString=120
const roomsText = /^\s*[1-9][0-9]*\s*$/;
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/** The page's tabs (C16). */
export type PropertyTab = 'overview' | 'subscriptions' | 'contacts' | 'groups' | 'trail';

/** The chip's tone beside a word (never colour alone). */
type ChipTone = 'ok' | 'warn' | 'info' | null;

/** One membership of this hotel, with the group it belongs to. */
interface Membership {
  readonly group: PropertyGroup;
  readonly fromUtc: string;
  readonly toUtc: string | null;
  readonly isCurrent: boolean;
}

const tabs: readonly PropertyTab[] = ['overview', 'subscriptions', 'contacts', 'groups', 'trail'];
const isTab = (value: string): value is PropertyTab => (tabs as readonly string[]).includes(value);
const TRAIL_ROWS_IN_CARD = 5;
const LINE_ID_SHOWN = 8;
const TRAIL_ROWS_IN_FULL = 25;
const dateFormat: Intl.DateTimeFormatOptions = {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
};
const shortDateFormat: Intl.DateTimeFormatOptions = {
  day: 'numeric',
  month: 'short',
  timeZone: 'UTC',
};

/** What the update form holds; the number of rooms and the dates travel as text until sent. */
interface UpdateValues {
  readonly nameEn: string;
  readonly nameAr: string;
  readonly addressLine: string;
  readonly postalCode: string;
  readonly totalRooms: string;
  readonly opensOn: string;
  readonly closesOn: string;
}

const fieldOfCode: Readonly<Record<string, keyof UpdateValues>> = {
  '10603': 'nameEn',
  '10604': 'nameEn',
  '10605': 'nameAr',
  '10606': 'nameAr',
  '10611': 'totalRooms',
  '10621': 'addressLine',
  '10613': 'addressLine',
  '10614': 'postalCode',
};

const empty: UpdateValues = {
  nameEn: '',
  nameAr: '',
  addressLine: '',
  postalCode: '',
  totalRooms: '',
  opensOn: '',
  closesOn: '',
};

/** A reference row's text under a key; '' when the row has none. */
const textOf = (row: ReferenceRow, key: string): string => {
  const value = row[key];
  return typeof value === 'string' ? value : '';
};

/**
 * One hotel, the contract's view (platform console design, C16): the head says what and where it is and whose it is;
 * the licence gap is said in a message bar, never refused; the overview holds the licence, the contacts and the
 * groups, then the subscription lines with their numbers and the trail. The part the console may change (names,
 * address, rooms, dates) is edited in the drawer. A new line is bought on the customer's page, which this one opens
 * already pointed at the hotel.
 */
@Component({
  selector: 'hr-property-page',
  imports: [
    NgTemplateOutlet,
    RouterLink,
    FormField,
    IfAllowed,
    Icon,
    PageHead,
    PopAt,
    Drawer,
    TrailCard,
  ],
  providers: [TrailStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './property-page.html',
  styleUrl: './property-page.css',
})
export class PropertyPage {
  /** From the route (:id), bound by the router. */
  readonly id = input.required<string>();
  /** ?tab=: the tab to open on. */
  readonly tab = input<string>();

  private readonly store = inject(PropertiesStore);
  protected readonly groups = inject(GroupsStore);
  private readonly reference = inject(ReferenceStore);
  private readonly tenants = inject(TenantChoicesStore);
  protected readonly trail = inject(TrailStore);
  private readonly toasts = inject(Toasts);
  private readonly drawer = viewChild.required(Drawer);
  private readonly locale = $localize.locale ?? 'en';
  private readonly dates = new Intl.DateTimeFormat(
    this.locale === 'en' ? 'en-GB' : this.locale,
    dateFormat,
  );
  private readonly shortDates = new Intl.DateTimeFormat(
    this.locale === 'en' ? 'en-GB' : this.locale,
    shortDateFormat,
  );
  private readonly cities = this.reference.choices('City');
  private readonly types = this.reference.choices('PropertyType');
  private readonly modules = this.reference.choices('Module');
  private readonly languages = this.reference.choices('Language');

  protected readonly manageProperties = Permission.ManageProperties;
  protected readonly property = signal<Property | null>(null);
  protected readonly loading = signal(true);
  protected readonly problem = signal<string | null>(null);
  protected readonly current = signal<PropertyTab>('overview');
  protected readonly editing = signal(false);
  protected readonly editProblem = signal<string | null>(null);
  protected readonly trailTitle = $localize`:@@platform-admin.property.trail.title:Trail for this hotel`;
  protected readonly trailRows = computed(() =>
    this.trail
      .rows()
      .slice(0, this.current() === 'trail' ? TRAIL_ROWS_IN_FULL : TRAIL_ROWS_IN_CARD),
  );
  /** This hotel's memberships across the customer's groups, current ones first. */
  protected readonly memberships = computed<readonly Membership[]>(() => {
    const id = this.id();
    return this.groups
      .rows()
      .flatMap((group) =>
        group.members
          .filter((member) => member.propertyId === id)
          .map((member) => ({
            group,
            fromUtc: member.fromUtc,
            toUtc: member.toUtc,
            isCurrent: member.isCurrent,
          })),
      )
      .sort((a, b) => Number(b.isCurrent) - Number(a.isCurrent));
  });
  protected readonly currentMemberships = computed(() =>
    this.memberships().filter((membership) => membership.isCurrent),
  );
  /** The brand the hotel carries now, if any (one brand at a time). */
  protected readonly brand = computed(
    () =>
      this.currentMemberships().find((membership) => membership.group.brand !== null)?.group ??
      null,
  );

  /** The editable copy of what may change; a fresh property resets it. */
  private readonly model = linkedSignal<UpdateValues>(() => {
    const property = this.property();
    return property === null
      ? empty
      : {
          nameEn: property.nameEn,
          nameAr: property.nameAr,
          addressLine: property.addressLine,
          postalCode: property.postalCode ?? '',
          totalRooms: String(property.totalRooms),
          opensOn: property.opensOn ?? '',
          closesOn: property.closesOn ?? '',
        };
  });
  protected readonly form = form(this.model, (path) => {
    required(path.nameEn, { message: wording('10603') });
    required(path.nameAr, { message: wording('10605') });
    required(path.addressLine, { message: wording('10621') });
    required(path.totalRooms, { message: wording('10611') });
    pattern(path.totalRooms, roomsText, { message: wording('10611') });
  });

  constructor() {
    this.cities.load();
    this.types.load();
    this.modules.load();
    this.languages.load();
    effect(() => {
      const id = this.id();
      untracked(() => {
        void this.read(id);
      });
    });
    effect(() => {
      const property = this.property();
      untracked(() => {
        if (property === null) return;
        this.groups.ofTenant(property.tenantId);
        this.trail.pageSize.set(TRAIL_ROWS_IN_FULL);
        this.trail.showTenant(property.tenantId);
        this.trail.search(property.code);
      });
    });
    effect(() => {
      const wanted = this.tab();
      untracked(() => {
        if (wanted !== undefined && isTab(wanted)) this.current.set(wanted);
      });
    });
  }

  // ── the head ────────────────────────────────────────────────────────────────

  protected tenantCode(property: Property): string {
    return this.tenants.rows().find((row) => row.id === property.tenantId)?.code ?? '';
  }

  protected tenantName(property: Property): string {
    const row = this.tenants.rows().find((candidate) => candidate.id === property.tenantId);
    if (row === undefined) return '';
    return this.locale.startsWith('ar') && row.legalNameAr !== ''
      ? row.legalNameAr
      : row.legalNameEn;
  }

  protected typeName(property: Property): string {
    const row = this.types
      .rows()
      .find((candidate) => textOf(candidate, 'code') === property.propertyTypeCode);
    return row === undefined ? property.propertyTypeCode : this.referenceName(row);
  }

  protected cityName(property: Property): string {
    const row = this.cities.rows().find((candidate) => textOf(candidate, 'id') === property.cityId);
    return row === undefined ? '' : this.referenceName(row);
  }

  protected languageName(property: Property): string {
    const row = this.languages
      .rows()
      .find((candidate) => textOf(candidate, 'code') === property.defaultLanguageCode);
    return row === undefined ? property.defaultLanguageCode : this.referenceName(row);
  }

  protected stateWord(property: Property): string {
    if (!property.isActive || this.closed(property))
      return $localize`:@@platform-admin.property.state.closed:Closed`;
    if (this.opensLater(property))
      return $localize`:@@platform-admin.property.state.opening:Opening`;
    return $localize`:@@platform-admin.property.state.active:Active`;
  }

  protected stateTone(property: Property): ChipTone {
    if (!property.isActive || this.closed(property)) return null;
    return this.opensLater(property) ? 'info' : 'ok';
  }

  /** Rooms declared above the licence; 0 when covered or unlicensed. */
  protected over(property: Property): number {
    if (property.licensedRooms === null || property.totalRooms <= property.licensedRooms) return 0;
    return property.totalRooms - property.licensedRooms;
  }

  protected date(day: string | null): string {
    return day === null ? '—' : this.dates.format(new Date(`${day}T00:00:00Z`));
  }

  protected show(tab: PropertyTab): void {
    this.current.set(tab);
  }

  // ── the licence card ────────────────────────────────────────────────────────

  protected licensed(property: Property): string {
    return property.licensedRooms === null ? '—' : String(property.licensedRooms);
  }

  /** This hotel's own lines carry the terminals; chain-wide lines are the customer's. */
  protected terminals(property: Property): string {
    const total = property.subscriptions
      .filter((line) => line.isEnabled && line.propertyId === property.id)
      .reduce((sum, line) => sum + (line.terminals ?? 0), 0);
    return total === 0
      ? '—'
      : $localize`:@@platform-admin.property.terminals:${String(total)}:count: licensed`;
  }

  protected regimes(property: Property): string {
    return [
      property.legalEnvironmentCode,
      property.taxEnvironmentCode,
      property.accountingEnvironmentCode,
      property.accommodationEnvironmentCode,
    ].join(' · ');
  }

  // ── the lines ───────────────────────────────────────────────────────────────

  protected moduleName(line: SubscriptionLine): string {
    const row = this.modules
      .rows()
      .find((candidate) => textOf(candidate, 'code') === line.moduleCode);
    return row === undefined ? line.moduleCode : `${line.moduleCode} · ${this.referenceName(row)}`;
  }

  /** The second line under the module: which line this is. */
  protected lineSub(line: SubscriptionLine): string {
    return line.propertyId === null
      ? $localize`:@@platform-admin.property.line.sub.whole:chain-wide line`
      : $localize`:@@platform-admin.property.line.sub.here:line ${line.id.slice(0, LINE_ID_SHOWN)}:id:`;
  }

  protected scopeOf(line: SubscriptionLine): string {
    return line.propertyId === null
      ? $localize`:@@platform-admin.property.line.whole:Whole tenant`
      : $localize`:@@platform-admin.property.line.here:This hotel`;
  }

  /** Identity's isHeld is "in force today"; an enabled line outside its dates has not started or has ended. */
  protected lineState(line: SubscriptionLine): string {
    if (!line.isEnabled) return $localize`:@@platform-admin.property.line.disabled:Disabled`;
    if (!line.isHeld)
      return this.endsByCalendar(line)
        ? $localize`:@@platform-admin.property.line.outside:Not in force`
        : $localize`:@@platform-admin.property.line.starts:Starts ${this.day(line.startsOn)}:date:`;
    if (this.endsByCalendar(line))
      return $localize`:@@platform-admin.property.line.ends:Ends ${this.day(line.endsOn ?? '')}:date:`;
    return $localize`:@@platform-admin.property.line.enabled:Active`;
  }

  protected lineTone(line: SubscriptionLine): ChipTone {
    if (!line.isEnabled) return null;
    if (!line.isHeld) return this.endsByCalendar(line) ? 'warn' : 'info';
    if (this.endsByCalendar(line)) return 'warn';
    return 'ok';
  }

  /** A line with an end date and no renewal stops by the calendar alone. */
  private endsByCalendar(line: SubscriptionLine): boolean {
    return line.endsOn !== null && !line.renewsAutomatically;
  }

  private day(iso: string): string {
    return this.shortDates.format(new Date(`${iso}T00:00:00Z`));
  }

  protected period(line: SubscriptionLine): string {
    const starts = this.date(line.startsOn);
    return line.endsOn === null
      ? $localize`:@@platform-admin.property.line.period.open:${starts}:starts: → open`
      : `${starts} → ${this.date(line.endsOn)}`;
  }

  /** The line's rooms; a hotel line smaller than the hotel says by how much. */
  protected lineOver(line: SubscriptionLine, property: Property): number {
    if (line.propertyId !== property.id || line.rooms === null) return 0;
    return property.totalRooms > line.rooms ? property.totalRooms - line.rooms : 0;
  }

  // ── groups ──────────────────────────────────────────────────────────────────

  protected membershipState(membership: Membership): string {
    return membership.isCurrent
      ? $localize`:@@platform-admin.property.groups.current:Current`
      : $localize`:@@platform-admin.property.groups.past:Past`;
  }

  protected instant(value: string | null): string {
    return value === null ? '—' : this.dates.format(new Date(value));
  }

  // ── the edit ────────────────────────────────────────────────────────────────

  protected reload(): void {
    void this.read(this.id());
  }

  protected openEditor(): void {
    this.editProblem.set(null);
    this.editing.set(true);
    this.drawer().open();
  }

  protected onSave(event: Event, property: Property): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.editProblem.set(null);
      const value = field().value();
      const update: PropertyUpdate = {
        nameEn: value.nameEn.trim(),
        nameAr: value.nameAr.trim(),
        addressLine: value.addressLine.trim(),
        postalCode: value.postalCode.trim(),
        totalRooms: Number(value.totalRooms.trim()),
        opensOn: value.opensOn === '' ? null : value.opensOn,
        closesOn: value.closesOn === '' ? null : value.closesOn,
      };
      const result = await this.store.update(property, update, newIntentKey());
      return this.settle(result, field);
    });
  }

  private async read(id: string): Promise<void> {
    this.loading.set(true);
    const result = await this.store.detail(id);
    if (result.kind === 'ok') {
      this.property.set(result.data);
      this.problem.set(null);
    } else {
      this.problem.set(messageOf(result));
    }
    this.loading.set(false);
  }

  private settle(
    result: ApiResult<unknown>,
    field: typeof this.form,
  ): { kind: string; message: string; fieldTree: typeof field.nameEn }[] | undefined {
    if (result.kind === 'ok') {
      this.toasts.show($localize`:@@platform-admin.property.toast.saved:Property saved.`);
      this.drawer().close();
      this.reload();
      return undefined;
    }
    if ('codes' in result) {
      const onFields = result.codes.flatMap((code) => {
        const name = fieldOfCode[code];
        return name === undefined
          ? []
          : [{ kind: `server-${code}`, message: wording(code), fieldTree: field[name] }];
      });
      if (onFields.length > 0) return onFields;
    }
    this.editProblem.set(messageOf(result));
    return undefined;
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
