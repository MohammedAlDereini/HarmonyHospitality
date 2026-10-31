import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormField, email, form, required, submit } from '@angular/forms/signals';
import {
  CompanyLoginsStore,
  GroupsStore,
  PropertiesStore,
  ReferenceStore,
  TenantDirectoryStore,
  TenantsStore,
} from '@harmony/platform-admin/data-access';
import {
  Property,
  PropertyGroup,
  ReferenceRow,
  SubscriptionLine,
  TENANT_MAX_TIER,
  Tenant,
  TenantStatus,
} from '@harmony/platform-admin/domain';
import { TrailCard } from '@harmony/platform-admin/ui';
import { TrailStore } from '@harmony/platform-operations/data-access';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { initialsOf } from '@harmony/shared/domain';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead, PopAt } from '@harmony/ui/shell';
import { CompanyLoginPanel } from './company-login-panel';
import { DirectoryPanel } from './directory-panel';
import { ReasonForm } from './reason-form';
import { SubscriptionEditor } from './subscription-editor';
import { TenantEditor } from './tenant-editor';
import { statusLabel } from './tenant-wording';

/** What the drawer holds: one thing at a time. */
type Opened = 'edit' | 'suspend' | 'invite' | 'subscribe';

/** The page's tabs (C3). */
export type TenantTab =
  | 'overview'
  | 'hotels'
  | 'groups'
  | 'subscriptions'
  | 'company-login'
  | 'directory'
  | 'people'
  | 'trail';

/** The chip's tone beside a word (never colour alone). */
type ChipTone = 'ok' | 'warn' | 'stop' | 'info' | null;

const DAY_MS = 86_400_000;
const TRAIL_ROWS_IN_CARD = 5;
const TRAIL_ROWS_IN_FULL = 25;
const tabs: readonly TenantTab[] = [
  'overview',
  'hotels',
  'groups',
  'subscriptions',
  'company-login',
  'directory',
  'people',
  'trail',
];
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');
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
const instantFormat: Intl.DateTimeFormatOptions = {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'UTC',
};

/** What the URL asks the page to open: `?invite=1` wins over `?subscribe=<hotel id>`; the key tells one request from the next. */
interface UrlAsk {
  readonly what: Opened;
  readonly key: string;
}

function askedBy(invite: string | undefined, subscribe: string | undefined): UrlAsk | null {
  if (invite === '1') return { what: 'invite', key: 'invite' };
  if (subscribe !== undefined) return { what: 'subscribe', key: `subscribe:${subscribe}` };
  return null;
}

const headings: Readonly<Record<Opened, string>> = {
  edit: $localize`:@@platform-admin.tenants.edit.title:Edit tenant`,
  suspend: $localize`:@@platform-admin.tenant.suspend.title:Suspend tenant`,
  invite: $localize`:@@platform-admin.tenant.invite.title:Invite the administrator`,
  subscribe: $localize`:@@platform-admin.subscriptions.new.title:Add module`,
};

/** A reference row's text under a key; '' when the row has none. */
const textOf = (row: ReferenceRow, key: string): string => {
  const value = row[key];
  return typeof value === 'string' ? value : '';
};

/** The locale Intl formats with: English dates read day, month, year. */
const formatLocale = (locale: string): string => (locale === 'en' ? 'en-GB' : locale);

/**
 * One customer (platform console design, C3): the head says who they are and where they stand; the tabs hold the
 * overview cards (administrator, subscription, how their people sign in), the subscription lines, the hotels, the
 * groups, the company login, the directory, the people count and the trail. Every write happens in the drawer at the
 * end of the page, one press with its own intent key; the page reads the tenant again afterwards and says what
 * happened in a toast. Everything shown comes from a door; what has none (billing) is not here.
 */
@Component({
  selector: 'hr-tenant-page',
  imports: [
    NgTemplateOutlet,
    RouterLink,
    FormField,
    IfAllowed,
    Icon,
    PageHead,
    PopAt,
    TenantEditor,
    ReasonForm,
    SubscriptionEditor,
    Drawer,
    CompanyLoginPanel,
    DirectoryPanel,
    TrailCard,
  ],
  providers: [CompanyLoginsStore, TenantDirectoryStore, TrailStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tenant-page.html',
  styleUrl: './tenant-page.css',
})
export class TenantPage {
  /** From the route (:id), bound by the router. */
  readonly id = input.required<string>();
  /** ?tab=: the tab to open on. */
  readonly tab = input<string>();
  /** ?invite=1: the list's row menu opens the invitation straight away. */
  readonly invite = input<string>();
  /** ?subscribe=<hotel id>: a hotel's page asks for a new line on that hotel. */
  readonly subscribe = input<string>();

  private readonly store = inject(TenantsStore);
  protected readonly properties = inject(PropertiesStore);
  protected readonly groups = inject(GroupsStore);
  private readonly reference = inject(ReferenceStore);
  protected readonly companyLogins = inject(CompanyLoginsStore);
  protected readonly directory = inject(TenantDirectoryStore);
  protected readonly trail = inject(TrailStore);
  private readonly toasts = inject(Toasts);
  private readonly injector = inject(Injector);
  private readonly drawer = viewChild.required(Drawer);
  /** The URL request already answered, so a reload of the tenant does not reopen the drawer. */
  private answered: string | null = null;
  private readonly locale = $localize.locale ?? 'en';
  private readonly dates = new Intl.DateTimeFormat(formatLocale(this.locale), dateFormat);
  private readonly shortDates = new Intl.DateTimeFormat(formatLocale(this.locale), shortDateFormat);
  private readonly instants = new Intl.DateTimeFormat(formatLocale(this.locale), instantFormat);
  private readonly modules = this.reference.choices('Module');
  private readonly cities = this.reference.choices('City');
  private readonly countries = this.reference.choices('Country');

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly maxTier = TENANT_MAX_TIER;
  protected readonly trailTitle = $localize`:@@platform-admin.tenant.trail.title:Trail for this tenant`;
  /** The newest five on the overview, a page of them on the Trail tab. */
  protected readonly trailRows = computed(() =>
    this.trail
      .rows()
      .slice(0, this.current() === 'trail' ? TRAIL_ROWS_IN_FULL : TRAIL_ROWS_IN_CARD),
  );
  protected readonly tenant = signal<Tenant | null>(null);
  protected readonly loading = signal(true);
  protected readonly problem = signal<string | null>(null);
  protected readonly current = signal<TenantTab>('overview');
  protected readonly opened = signal<Opened | null>(null);
  /** The line whose unsubscribe reason is being typed, if any. */
  protected readonly ending = signal<string | null>(null);
  protected readonly busy = signal(false);
  /** One sentence when a press was refused; null while all is well. */
  protected readonly actionProblem = signal<string | null>(null);
  /** Said once after the invitation went out. */
  protected readonly invited = signal(false);
  protected readonly drawerHeading = computed(() => {
    const opened = this.opened();
    if (opened !== null) return headings[opened];
    return this.ending() === null
      ? ''
      : $localize`:@@platform-admin.subscriptions.end.title:End subscription`;
  });
  protected readonly drawerLead = computed(() =>
    this.opened() === 'invite'
      ? $localize`:@@platform-admin.tenant.invite.note:A new invitation goes to this person; leave both empty to send it again to the one already known.`
      : '',
  );
  protected readonly hotels = computed<readonly Property[]>(() => this.properties.rows());
  protected readonly groupRows = computed<readonly PropertyGroup[]>(() => this.groups.rows());

  private readonly inviteModel = signal({ administratorEmail: '', administratorDisplayName: '' });
  // Both empty means "send it again to the one already known" (the drawer says so): a person is named with both fields
  // or with neither, so each is required only once the other is typed.
  protected readonly inviteForm = form(this.inviteModel, (path) => {
    required(path.administratorEmail, {
      message: wording('10516'),
      when: () => this.inviteModel().administratorDisplayName.trim() !== '',
    });
    email(path.administratorEmail, { message: wording('10517') });
    required(path.administratorDisplayName, {
      message: wording('10519'),
      when: () => this.inviteModel().administratorEmail.trim() !== '',
    });
  });

  protected readonly suspendLabel = $localize`:@@platform-admin.tenants.suspend.reason:Reason for the suspension`;
  protected readonly suspendPlaceholder = $localize`:@@platform-admin.tenants.suspend.placeholder:Why this tenant is being suspended`;
  protected readonly suspendGo = $localize`:@@platform-admin.tenants.suspend.go:Suspend tenant`;
  protected readonly unsubscribeLabel = $localize`:@@platform-admin.subscriptions.end.reason:Reason for ending the subscription`;
  protected readonly unsubscribePlaceholder = $localize`:@@platform-admin.subscriptions.end.placeholder:Why this module is being taken away`;
  protected readonly unsubscribeGo = $localize`:@@platform-admin.subscriptions.end.go:End subscription`;

  constructor() {
    this.modules.load();
    this.cities.load();
    this.countries.load();
    effect(() => {
      const id = this.id();
      untracked(() => {
        this.properties.ofTenant(id);
        this.groups.ofTenant(id);
        this.companyLogins.ofTenant(id);
        this.directory.ofTenant(id);
        this.trail.pageSize.set(TRAIL_ROWS_IN_FULL);
        this.trail.showTenant(id);
        void this.read(id);
      });
    });
    effect(() => {
      const wanted = this.tab();
      untracked(() => {
        if (wanted !== undefined && isTab(wanted)) this.current.set(wanted);
      });
    });
    effect(() => {
      const ask = askedBy(this.invite(), this.subscribe());
      const tenant = this.tenant();
      untracked(() => {
        if (tenant === null || ask === null || ask.key === this.answered || this.opened() !== null)
          return;
        this.answered = ask.key;
        // The drawer is born with the tenant's view: it opens once that view has rendered, not inside this pass.
        afterNextRender(
          () => {
            this.open(ask.what);
          },
          { injector: this.injector },
        );
      });
    });
  }

  // ── the head ────────────────────────────────────────────────────────────────

  protected status(tenant: Tenant): string {
    return statusLabel(tenant);
  }

  protected statusTone(tenant: Tenant): ChipTone {
    if (tenant.status === TenantStatus.active) return 'ok';
    if (tenant.status === TenantStatus.suspended) return 'stop';
    return null;
  }

  protected countryName(tenant: Tenant): string {
    const row = this.countries
      .rows()
      .find((candidate) => textOf(candidate, 'code') === tenant.countryCode);
    return row === undefined ? (tenant.countryCode ?? '') : this.referenceName(row);
  }

  protected date(day: string | null): string {
    return day === null ? '—' : this.dates.format(new Date(`${day}T00:00:00Z`));
  }

  protected isActive(tenant: Tenant): boolean {
    return tenant.status === TenantStatus.active;
  }

  protected isSuspended(tenant: Tenant): boolean {
    return tenant.status === TenantStatus.suspended;
  }

  // ── the tabs ────────────────────────────────────────────────────────────────

  protected show(tab: TenantTab): void {
    this.current.set(tab);
  }

  // ── the administrator card ──────────────────────────────────────────────────

  protected initials(tenant: Tenant): string {
    return tenant.administrator === null ? '—' : initialsOf(tenant.administrator.displayName);
  }

  protected twoStep(tenant: Tenant): string {
    const administrator = tenant.administrator;
    const off = $localize`:@@platform-admin.tenant.administrator.two-step.off:Not set up`;
    if (administrator === null) return off;
    if (!administrator.twoStepEnrolled) return off;
    return $localize`:@@platform-admin.tenant.administrator.two-step.on:On · ${this.methodWord(administrator.twoStepMethod)}:method:`;
  }

  protected lastSignIn(tenant: Tenant): string {
    const administrator = tenant.administrator;
    const never = $localize`:@@platform-admin.tenant.administrator.never:Never`;
    if (administrator === null) return never;
    if (administrator.lastSignInUtc === null) return never;
    const when = this.instants.format(new Date(administrator.lastSignInUtc));
    const methods = administrator.lastSignInMethods ?? '';
    return methods === '' ? `${when} UTC` : `${when} UTC · ${methods}`;
  }

  protected invitation(tenant: Tenant): string {
    const administrator = tenant.administrator;
    if (administrator === null) return '—';
    if (administrator.invitedOn === null) return '—';
    const days = this.daysSince(administrator.invitedOn);
    if (administrator.invitationPending)
      return $localize`:@@platform-admin.tenant.administrator.pending:Not accepted · ${days}:days: d`;
    if (administrator.invitationExpired)
      return $localize`:@@platform-admin.tenant.administrator.expired:Expired · ${days}:days: d`;
    return $localize`:@@platform-admin.tenant.administrator.accepted:Accepted`;
  }

  protected invitationTone(tenant: Tenant): ChipTone {
    const administrator = tenant.administrator;
    if (administrator === null) return null;
    if (administrator.invitedOn === null) return null;
    if (administrator.invitationPending) return 'warn';
    if (administrator.invitationExpired) return 'stop';
    return 'ok';
  }

  // ── the subscription card ───────────────────────────────────────────────────

  protected enabledLines(tenant: Tenant): readonly SubscriptionLine[] {
    return tenant.subscriptions.filter((line) => line.isEnabled);
  }

  /** "740 across 6 hotels · 712 in use". */
  protected licence(tenant: Tenant): string {
    return $localize`:@@platform-admin.tenant.licence:${String(tenant.licensedRooms)}:licensed: across ${String(tenant.hotels)}:hotels: hotels · ${String(tenant.roomsInUse)}:inUse: in use`;
  }

  /** "FO · HK · RA chain-wide · FIN on AMM-GRD only". */
  protected moduleSummary(tenant: Tenant): string {
    const lines = this.enabledLines(tenant);
    const chainWide = [
      ...new Set(lines.filter((line) => line.propertyId === null).map((line) => line.moduleCode)),
    ];
    const perHotel = new Map<string, Set<string>>();
    lines
      .filter((line) => line.propertyId !== null && !chainWide.includes(line.moduleCode))
      .forEach((line) => {
        const hotels = perHotel.get(line.moduleCode) ?? new Set<string>();
        hotels.add(this.hotelCode(line.propertyId ?? ''));
        perHotel.set(line.moduleCode, hotels);
      });
    const parts: string[] = [];
    if (chainWide.length > 0)
      parts.push(
        $localize`:@@platform-admin.tenant.modules.chain-wide:${chainWide.join(' · ')}:modules: chain-wide`,
      );
    [...perHotel.entries()].forEach(([module, hotels]) => {
      parts.push(
        $localize`:@@platform-admin.tenant.modules.on:${module}:module: on ${[...hotels].join(', ')}:hotels: only`,
      );
    });
    return parts.length === 0 ? '—' : parts.join(' · ');
  }

  /** The earliest end among the enabled lines, and whether it renews. */
  protected renewal(tenant: Tenant): string {
    const ending = this.enabledLines(tenant)
      .filter((line) => line.endsOn !== null)
      .sort((a, b) => (a.endsOn ?? '').localeCompare(b.endsOn ?? ''));
    const first = ending[0];
    if (first === undefined) return $localize`:@@platform-admin.tenant.renewal.open:Open-ended`;
    const when = this.date(first.endsOn);
    return first.renewsAutomatically
      ? $localize`:@@platform-admin.tenant.renewal.renews:${when}:date: · renews automatically`
      : $localize`:@@platform-admin.tenant.renewal.fixed:${when}:date: · fixed term`;
  }

  protected terminals(tenant: Tenant): string {
    const total = this.enabledLines(tenant).reduce((sum, line) => sum + (line.terminals ?? 0), 0);
    return total === 0
      ? '—'
      : $localize`:@@platform-admin.tenant.terminals:${String(total)}:count: EPOS terminals licensed`;
  }

  protected renews(tenant: Tenant): boolean {
    return this.enabledLines(tenant).some((line) => line.renewsAutomatically);
  }

  // ── how their people sign in ────────────────────────────────────────────────

  protected companyLogin(): string {
    const rows = this.companyLogins.rows();
    if (rows.length === 0)
      return $localize`:@@platform-admin.tenant.sign-in.company.none:Not set up`;
    return rows
      .map((row) =>
        row.isEnabled
          ? $localize`:@@platform-admin.tenant.sign-in.company.on:${row.provider}:provider: · enabled · ${row.domains.join(', ')}:domains:`
          : $localize`:@@platform-admin.tenant.sign-in.company.off:${row.provider}:provider: · switched off`,
      )
      .join(' · ');
  }

  protected directoryLine(): string {
    const directory = this.directory.directory();
    if (directory === null)
      return $localize`:@@platform-admin.tenant.sign-in.directory.none:No directory`;
    const state = directory.isEnabled
      ? $localize`:@@platform-admin.tenant.sign-in.directory.on:SCIM push on`
      : $localize`:@@platform-admin.tenant.sign-in.directory.off:SCIM push off`;
    return $localize`:@@platform-admin.tenant.sign-in.directory.line:${state}:state: · ${String(directory.people)}:people: people · ${String(directory.groups.length)}:groups: groups`;
  }

  protected twoStepLine(tenant: Tenant): string {
    return tenant.twoStepExceptions === 0
      ? $localize`:@@platform-admin.tenant.sign-in.two-step.all:Required for everyone`
      : $localize`:@@platform-admin.tenant.sign-in.two-step.some:Required · ${String(tenant.twoStepExceptions)}:count: exceptions`;
  }

  protected passwordsLine(tenant: Tenant): string {
    return $localize`:@@platform-admin.tenant.sign-in.passwords:${String(tenant.peopleWithPassword)}:count: of ${String(tenant.people)}:people: people keep a password`;
  }

  // ── the lines ───────────────────────────────────────────────────────────────

  protected moduleName(line: SubscriptionLine): string {
    const row = this.modules
      .rows()
      .find((candidate) => textOf(candidate, 'code') === line.moduleCode);
    return row === undefined ? line.moduleCode : `${line.moduleCode} · ${this.referenceName(row)}`;
  }

  protected moduleLevel(line: SubscriptionLine): string {
    const row = this.modules
      .rows()
      .find((candidate) => textOf(candidate, 'code') === line.moduleCode);
    const level = row === undefined ? '' : textOf(row, 'level');
    return level === ''
      ? ''
      : $localize`:@@platform-admin.tenant.lines.level:Level: ${level}:level:`;
  }

  protected scopeOf(line: SubscriptionLine, tenant: Tenant): string {
    if (line.propertyId === null)
      return $localize`:@@platform-admin.subscriptions.scope.whole:Chain-wide · all ${String(tenant.hotels)}:hotels: hotels`;
    const hotel = this.hotels().find((candidate) => candidate.id === line.propertyId);
    return hotel === undefined
      ? $localize`:@@platform-admin.subscriptions.scope.property:One hotel`
      : `${hotel.code} · ${hotel.nameEn}`;
  }

  /** Identity's isHeld is "in force today"; an enabled line outside its dates has not started or has ended. */
  protected lineState(line: SubscriptionLine): string {
    if (!line.isEnabled) return $localize`:@@platform-admin.subscriptions.state.disabled:Disabled`;
    if (!line.isHeld)
      return this.endsByCalendar(line)
        ? $localize`:@@platform-admin.subscriptions.state.outside:Not in force`
        : $localize`:@@platform-admin.subscriptions.state.starts:Starts ${this.shortDate(line.startsOn)}:date:`;
    if (this.endsByCalendar(line))
      return $localize`:@@platform-admin.subscriptions.state.ends:Ends ${this.shortDate(line.endsOn ?? '')}:date:`;
    return $localize`:@@platform-admin.subscriptions.state.enabled:Active`;
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

  protected period(line: SubscriptionLine): string {
    const starts = this.date(line.startsOn);
    return line.endsOn === null
      ? $localize`:@@platform-admin.tenant.lines.period.open:${starts}:starts: → open`
      : `${starts} → ${this.date(line.endsOn)}`;
  }

  /** The line whose End form is in the drawer, if any. */
  protected endingLine(tenant: Tenant): SubscriptionLine | null {
    const id = this.ending();
    return id === null ? null : (tenant.subscriptions.find((line) => line.id === id) ?? null);
  }

  // ── the hotels ──────────────────────────────────────────────────────────────

  protected cityOf(hotel: Property): string {
    const row = this.cities.rows().find((candidate) => textOf(candidate, 'id') === hotel.cityId);
    const city = row === undefined ? '' : this.referenceName(row);
    return city === '' ? hotel.timeZoneId : `${city} · ${hotel.timeZoneId}`;
  }

  /** "132 · 130": licensed today, then declared. */
  protected hotelRooms(hotel: Property): string {
    const licensed = hotel.licensedRooms === null ? '—' : String(hotel.licensedRooms);
    return `${licensed} · ${String(hotel.totalRooms)}`;
  }

  protected hotelOver(hotel: Property): number {
    if (hotel.licensedRooms === null || hotel.totalRooms <= hotel.licensedRooms) return 0;
    return hotel.totalRooms - hotel.licensedRooms;
  }

  protected hotelState(hotel: Property): string {
    if (!hotel.isActive) return $localize`:@@platform-admin.tenant.hotels.closed:Closed`;
    if (this.opensLater(hotel)) return $localize`:@@platform-admin.tenant.hotels.opening:Opening`;
    return $localize`:@@platform-admin.tenant.hotels.active:Active`;
  }

  protected hotelTone(hotel: Property): ChipTone {
    if (!hotel.isActive) return null;
    return this.opensLater(hotel) ? 'info' : 'ok';
  }

  // ── the writes ──────────────────────────────────────────────────────────────

  protected reload(): void {
    void this.read(this.id());
  }

  protected open(what: Opened): void {
    this.actionProblem.set(null);
    this.invited.set(false);
    this.ending.set(null);
    this.opened.set(what);
    this.drawer().open();
  }

  /** The drawer has closed, whatever closed it: nothing is open any more. */
  protected drawerClosed(): void {
    this.opened.set(null);
    this.ending.set(null);
  }

  protected changed(): void {
    this.toasts.show(
      this.opened() === 'subscribe'
        ? $localize`:@@platform-admin.tenant.toast.subscribed:Module added.`
        : $localize`:@@platform-admin.tenant.toast.saved:Tenant saved.`,
    );
    this.drawer().close();
    this.reload();
    this.properties.reload();
  }

  protected askEnd(line: SubscriptionLine): void {
    this.actionProblem.set(null);
    this.opened.set(null);
    this.ending.set(line.id);
    this.drawer().open();
  }

  protected suspend(tenant: Tenant, reason: string): void {
    void this.press(this.store.suspend(tenant, reason, newIntentKey()), () => {
      this.toasts.show($localize`:@@platform-admin.tenant.toast.suspended:Tenant suspended.`);
      this.drawer().close();
    });
  }

  protected reinstate(tenant: Tenant): void {
    void this.press(this.store.reinstate(tenant, newIntentKey()), () => {
      this.toasts.show($localize`:@@platform-admin.tenant.toast.reinstated:Tenant reinstated.`);
    });
  }

  protected endLine(tenant: Tenant, line: SubscriptionLine, reason: string): void {
    void this.press(this.store.unsubscribe(tenant, line, reason, newIntentKey()), () => {
      this.toasts.show($localize`:@@platform-admin.tenant.toast.unsubscribed:Subscription ended.`);
      this.drawer().close();
    });
  }

  protected onInvite(event: Event, tenant: Tenant): void {
    event.preventDefault();
    void submit(this.inviteForm, async (field) => {
      const value = field().value();
      await this.press(
        this.store.invite(
          tenant,
          value.administratorEmail.trim(),
          value.administratorDisplayName.trim(),
          newIntentKey(),
        ),
        () => {
          this.toasts.show($localize`:@@platform-admin.tenant.invited:The invitation went out.`);
          this.drawer().close();
          this.invited.set(true);
        },
      );
      return undefined;
    });
  }

  private async read(id: string): Promise<void> {
    this.loading.set(true);
    const result = await this.store.detail(id);
    if (result.kind === 'ok') {
      this.tenant.set(result.data);
      this.problem.set(null);
    } else {
      this.problem.set(messageOf(result));
    }
    this.loading.set(false);
  }

  /** One press: nothing else until it settles; success reads the tenant again; a refusal is worded once. */
  private async press(call: Promise<ApiResult<unknown>>, after?: () => void): Promise<void> {
    this.actionProblem.set(null);
    this.busy.set(true);
    const result = await call;
    this.busy.set(false);
    if (result.kind === 'ok') {
      after?.();
      this.reload();
      this.properties.reload();
      return;
    }
    this.actionProblem.set(messageOf(result));
  }

  private opensLater(hotel: Property): boolean {
    return hotel.opensOn !== null && new Date(`${hotel.opensOn}T00:00:00Z`).getTime() > Date.now();
  }

  private hotelCode(propertyId: string): string {
    return this.hotels().find((hotel) => hotel.id === propertyId)?.code ?? '?';
  }

  private referenceName(row: ReferenceRow): string {
    const arabic = textOf(row, 'nameAr');
    return this.locale.startsWith('ar') && arabic !== '' ? arabic : textOf(row, 'nameEn');
  }

  private methodWord(method: string): string {
    const words: Readonly<Record<string, string>> = {
      AuthenticatorApp: $localize`:@@platform-admin.tenant.two-step.app:authenticator app`,
      EmailLink: $localize`:@@platform-admin.tenant.two-step.mail:e-mail link`,
      Passkey: $localize`:@@platform-admin.tenant.two-step.passkey:passkey`,
    };
    return words[method] ?? method;
  }

  private shortDate(day: string): string {
    return this.shortDates.format(new Date(`${day}T00:00:00Z`));
  }

  private daysSince(instant: string): number {
    const elapsed = Date.now() - new Date(instant).getTime();
    return Math.max(0, Math.floor(elapsed / DAY_MS));
  }
}

const isTab = (value: string): value is TenantTab => (tabs as readonly string[]).includes(value);
