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
import {
  ActorChoicesStore,
  TRAIL_PAGE_SIZE,
  TenantChoicesStore,
  TrailStore,
} from '@harmony/platform-operations/data-access';
import {
  ActorChoice,
  SYSTEM_ACTOR,
  TRAIL_FAMILIES,
  TenantChoice,
  TrailEvent,
  TrailFamily,
  TrailTone,
  daysAgo,
  familyWording,
  hasTrailWording,
  tenantChoiceName,
  trailCsv,
  trailCsvFileName,
  trailTone,
  trailWording,
  utcDay,
  utcTime,
} from '@harmony/platform-operations/domain';
import { SessionStore } from '@harmony/platform/session';
import { initialsOf } from '@harmony/shared/domain';
import { Pager, saveTextFile } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead } from '@harmony/ui/shell';
import { TrailEventDetail } from '@harmony/platform-operations/ui';

/** How far back the trail is read: the Figma's "Date" filter. */
export type TrailWindow = '24h' | '7d' | '30d' | 'all';

interface WindowChoice {
  readonly value: TrailWindow;
  readonly days: number | null;
  readonly label: string;
}

const DAY = 1;
const WEEK = 7;
const MONTH = 30;
/** Grey rows while the first page loads. */
const SKELETON_ROWS = 5;

/**
 * The platform's audit log (Figma C8): the trail across every customer, newest first, with a search, a window, a
 * customer, an actor and an event family to narrow by; a row opens in a drawer with every fact Identity kept. Reads only.
 */
@Component({
  selector: 'hr-trail-page',
  imports: [Icon, PageHead, Pager, Drawer, TrailEventDetail],
  providers: [TrailStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trail-page.html',
  styleUrl: './trail-page.css',
})
export class TrailPage {
  protected readonly store = inject(TrailStore);
  protected readonly tenants = inject(TenantChoicesStore);
  protected readonly actors = inject(ActorChoicesStore);
  private readonly session = inject(SessionStore);
  private readonly toasts = inject(Toasts);
  private readonly document = inject(DOCUMENT);
  private readonly drawer = viewChild.required(Drawer);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly pageSize = TRAIL_PAGE_SIZE;
  /** Grey rows while the first page loads. */
  protected readonly skeleton: readonly number[] = Array.from(
    { length: SKELETON_ROWS },
    (_, index) => index,
  );
  protected readonly families: readonly TrailFamily[] = TRAIL_FAMILIES;
  /** ?tenant=<id>: a customer's page opens the log already narrowed to them. */
  readonly tenant = input<string>();
  protected readonly selected = signal<TrailEvent | null>(null);
  protected readonly window = signal<TrailWindow>('7d');
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly tenantValue = computed(() => this.store.filters().tenantId ?? '');
  protected readonly familyValue = computed(() => this.store.filters().action ?? '');
  protected readonly actorValue = computed(() => this.store.filters().actorSubject ?? '');
  /** Identity's own doings, as the actor filter names them. */
  protected readonly system = SYSTEM_ACTOR;
  /** The platform's people by name, as the actor filter lists them. */
  protected readonly actorChoices = computed<readonly ActorChoice[]>(() =>
    [...this.actors.rows()].sort((a, b) => a.displayName.localeCompare(b.displayName, this.locale)),
  );
  protected readonly windows: readonly WindowChoice[] = [
    {
      value: '24h',
      days: DAY,
      label: $localize`:@@platform-operations.trail.window.day:Last 24 hours`,
    },
    {
      value: '7d',
      days: WEEK,
      label: $localize`:@@platform-operations.trail.window.week:Last 7 days`,
    },
    {
      value: '30d',
      days: MONTH,
      label: $localize`:@@platform-operations.trail.window.month:Last 30 days`,
    },
    {
      value: 'all',
      days: null,
      label: $localize`:@@platform-operations.trail.window.all:All time`,
    },
  ];
  protected readonly windowLabel = computed(
    () => this.windows.find((choice) => choice.value === this.window())?.label ?? '',
  );
  /** The platform's own tenant: the signed-in person's; only its people have a page in this console. */
  protected readonly ownTenant = computed(() => this.session.user()?.tenant ?? null);

  constructor() {
    this.applyWindow('7d');
    effect(() => {
      const tenant = this.tenant();
      if (tenant !== undefined) {
        untracked(() => {
          this.store.showTenant(tenant);
        });
      }
    });
  }

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.store.search(text);
  }

  protected onWindow(value: string): void {
    const choice = this.windows.find((candidate) => candidate.value === value);
    if (choice !== undefined) this.applyWindow(choice.value);
  }

  protected onTenant(value: string): void {
    this.store.showTenant(value === '' ? null : value);
  }

  protected onActor(value: string): void {
    this.store.showActor(value === '' ? null : value);
  }

  protected onFamily(value: string): void {
    this.store.showAction(value === '' ? null : value);
  }

  protected open(row: TrailEvent): void {
    this.selected.set(row);
    this.drawer().open();
  }

  /** The rows on screen as a CSV file the browser saves; nothing leaves the page but to the person's disk. */
  protected exportCsv(): void {
    const rows = this.store.rows();
    if (rows.length === 0) return;
    saveTextFile(
      this.document,
      trailCsvFileName(rows, 'harmony-trail'),
      trailCsv(rows),
      'text/csv;charset=utf-8',
    );
    this.toasts.show(
      $localize`:@@platform-operations.trail.exported:${rows.length}:count: events exported.`,
    );
  }

  protected time(row: TrailEvent): string {
    return utcTime(row.occurredAtUtc, this.locale);
  }

  protected day(row: TrailEvent): string {
    return utcDay(row.occurredAtUtc, this.locale, new Date());
  }

  /** The action in words, or the detail when the words would only repeat the code. */
  protected summary(row: TrailEvent): string {
    const words = hasTrailWording(row.action) ? trailWording(row.action) : '';
    const detail = row.detail ?? '';
    if (words === '') return detail;
    return detail === '' ? words : `${words} · ${detail}`;
  }

  protected familyName(family: TrailFamily): string {
    return familyWording(family);
  }

  protected tenantName(choice: TenantChoice): string {
    return tenantChoiceName(choice, this.locale);
  }

  protected isSystem(row: TrailEvent): boolean {
    return row.actorSubject === SYSTEM_ACTOR;
  }

  /** The actor's name, "system", or the id Identity kept when the account is gone. */
  protected actorName(row: TrailEvent): string {
    if (this.isSystem(row)) return $localize`:@@platform-operations.trail.actor.system:system`;
    return row.actorDisplayName ?? row.actorSubject;
  }

  /** Where the actor stands: the customer's code, or the service itself. */
  protected actorWhere(row: TrailEvent): string {
    if (this.isSystem(row))
      return $localize`:@@platform-operations.trail.actor.identity:Identity service`;
    return row.tenantCode ?? '';
  }

  protected initials(row: TrailEvent): string {
    return this.isSystem(row) ? '—' : initialsOf(row.actorDisplayName ?? '');
  }

  /** What the row is about: the person named, else the detail Identity wrote. */
  protected target(row: TrailEvent): string {
    return row.userDisplayName ?? row.detail ?? '';
  }

  protected tone(row: TrailEvent): TrailTone {
    return trailTone(row.action);
  }

  private applyWindow(value: TrailWindow): void {
    this.window.set(value);
    const choice = this.windows.find((candidate) => candidate.value === value);
    const days = choice?.days ?? null;
    this.store.between(days === null ? null : daysAgo(days, new Date()), null);
  }
}
