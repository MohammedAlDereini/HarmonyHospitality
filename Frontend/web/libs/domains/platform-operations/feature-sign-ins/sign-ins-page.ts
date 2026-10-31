import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import {
  MyDevicesStore,
  TRAIL_PAGE_SIZE,
  TenantChoicesStore,
  TrailStore,
} from '@harmony/platform-operations/data-access';
import {
  MyDevice,
  SIGN_IN_FAMILY,
  SIGN_IN_METHODS,
  SIGN_IN_OUTCOMES,
  SignInMethod,
  SignInOutcome,
  TenantChoice,
  TrailCount,
  TrailEvent,
  TrailTone,
  actionsOfMethod,
  countWhere,
  daysAgo,
  signInMethodOf,
  signInMethodWording,
  signInOutcomeOf,
  signInOutcomeWording,
  tenantChoiceName,
  trailCsv,
  trailCsvFileName,
  trailTone,
  utcDay,
  utcInstant,
  utcTime,
} from '@harmony/platform-operations/domain';
import { TrailEventDetail } from '@harmony/platform-operations/ui';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { SessionStore } from '@harmony/platform/session';
import { initialsOf } from '@harmony/shared/domain';
import { Pager, saveTextFile } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead } from '@harmony/ui/shell';

/** The windows the numbers compare: the last day against the day before it. */
const ONE_DAY = 1;
const TWO_DAYS = 2;
/** Grey rows while the first page loads. */
const SKELETON_ROWS = 5;

/** One of the four numbers at the top, with how it moved against the day before. */
interface SignInFigure {
  readonly label: string;
  readonly value: number;
  readonly delta: number;
  readonly note: string;
}

const signedIn = (action: string): boolean => signInOutcomeOf(action) === 'signed-in';
const refused = (action: string): boolean => signInOutcomeOf(action) === 'refused';
const held = (action: string): boolean => action === 'sign-in.new-device-held';
const trusted = (action: string): boolean => action === 'device.trusted';
const twoStep = (action: string): boolean => signInMethodOf(action) === 'two-step';
const wrongCode = (action: string): boolean => action === 'sign-in.wrong-code';

/**
 * Sign-ins across every customer in the last day (Figma C9): four numbers against the day before, the trail's
 * sign-in rows with the outcome, the customer and the method to narrow by, the person's own browsers to sign out of,
 * and the platform's sign-in policy as the service enforces it.
 */
@Component({
  selector: 'hr-sign-ins-page',
  imports: [Icon, PageHead, Pager, Drawer, TrailEventDetail],
  providers: [TrailStore, MyDevicesStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sign-ins-page.html',
  styleUrl: './sign-ins-page.css',
})
export class SignInsPage {
  protected readonly store = inject(TrailStore);
  protected readonly tenants = inject(TenantChoicesStore);
  protected readonly devices = inject(MyDevicesStore);
  private readonly session = inject(SessionStore);
  private readonly toasts = inject(Toasts);
  private readonly document = inject(DOCUMENT);
  private readonly drawer = viewChild.required(Drawer);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly pageSize = TRAIL_PAGE_SIZE;
  protected readonly methods: readonly SignInMethod[] = SIGN_IN_METHODS;
  protected readonly outcomes: readonly SignInOutcome[] = SIGN_IN_OUTCOMES;
  protected readonly skeleton: readonly number[] = Array.from(
    { length: SKELETON_ROWS },
    (_, index) => index,
  );
  protected readonly selected = signal<TrailEvent | null>(null);
  protected readonly outcome = signal<SignInOutcome | ''>('');
  protected readonly method = signal<SignInMethod | ''>('');
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly tenantValue = computed(() => this.store.filters().tenantId ?? '');
  protected readonly ownTenant = computed(() => this.session.user()?.tenant ?? null);
  /** Every action of the last day, and of the day before, for the four numbers. */
  protected readonly today = signal<readonly TrailCount[] | null>(null);
  protected readonly before = signal<readonly TrailCount[] | null>(null);
  protected readonly countsProblem = signal<string | null>(null);

  protected readonly figures = computed<readonly SignInFigure[] | null>(() => {
    const now = this.today();
    const then = this.before();
    if (now === null || then === null) return null;
    return [
      {
        label: $localize`:@@platform-operations.sign-ins.figure.signed-in:Sign-ins`,
        value: countWhere(now, signedIn),
        delta: countWhere(now, signedIn) - countWhere(then, signedIn),
        note: $localize`:@@platform-operations.sign-ins.figure.signed-in.note:${countWhere(now, (a) => signInMethodOf(a) === 'passkey')}:count: with a passkey`,
      },
      {
        label: $localize`:@@platform-operations.sign-ins.figure.refused:Refused`,
        value: countWhere(now, refused),
        delta: countWhere(now, refused) - countWhere(then, refused),
        note: $localize`:@@platform-operations.sign-ins.figure.refused.note:${countWhere(now, (a) => a === 'sign-in.locked-out')}:count: locked out`,
      },
      {
        label: $localize`:@@platform-operations.sign-ins.figure.held:New devices held`,
        value: countWhere(now, held),
        delta: countWhere(now, held) - countWhere(then, held),
        note: $localize`:@@platform-operations.sign-ins.figure.held.note:${countWhere(now, trusted)}:count: trusted from the mail`,
      },
      {
        label: $localize`:@@platform-operations.sign-ins.figure.two-step:Two-step sign-ins`,
        value: countWhere(now, twoStep),
        delta: countWhere(now, twoStep) - countWhere(then, twoStep),
        note: $localize`:@@platform-operations.sign-ins.figure.two-step.note:${countWhere(now, wrongCode)}:count: wrong codes`,
      },
    ];
  });

  constructor() {
    this.store.family.set(SIGN_IN_FAMILY);
    this.store.between(daysAgo(ONE_DAY, new Date()), null);
    void this.loadCounts();
  }

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.store.search(text);
  }

  protected onOutcome(value: string): void {
    this.outcome.set(this.outcomes.find((candidate) => candidate === value) ?? '');
    this.applyActions();
  }

  protected onMethod(value: string): void {
    this.method.set(this.methods.find((candidate) => candidate === value) ?? '');
    this.applyActions();
  }

  protected onTenant(value: string): void {
    this.store.showTenant(value === '' ? null : value);
  }

  protected open(row: TrailEvent): void {
    this.selected.set(row);
    this.drawer().open();
  }

  protected refresh(): void {
    this.store.reload();
    this.devices.reload();
    void this.loadCounts();
  }

  protected exportCsv(): void {
    const rows = this.store.rows();
    if (rows.length === 0) return;
    saveTextFile(
      this.document,
      trailCsvFileName(rows, 'harmony-sign-ins'),
      trailCsv(rows),
      'text/csv;charset=utf-8',
    );
    this.toasts.show(
      $localize`:@@platform-operations.sign-ins.exported:${rows.length}:count: sign-ins exported.`,
    );
  }

  protected async signOut(device: MyDevice): Promise<void> {
    const result = await this.devices.signOut(device, newIntentKey());
    if (result.kind === 'ok') {
      this.toasts.show(
        $localize`:@@platform-operations.sign-ins.signed-out:${device.label}:device: signed out.`,
      );
    } else {
      this.toasts.show(messageOf(result), true);
    }
  }

  protected deltaText(delta: number): string {
    if (delta === 0)
      return $localize`:@@platform-operations.sign-ins.delta.same:same as the day before`;
    return delta > 0
      ? $localize`:@@platform-operations.sign-ins.delta.up:+${delta}:count: vs the day before`
      : $localize`:@@platform-operations.sign-ins.delta.down:${delta}:count: vs the day before`;
  }

  protected time(row: TrailEvent): string {
    return utcTime(row.occurredAtUtc, this.locale);
  }

  protected day(row: TrailEvent): string {
    return utcDay(row.occurredAtUtc, this.locale, new Date());
  }

  protected methodOf(row: TrailEvent): string {
    return signInMethodWording(signInMethodOf(row.action));
  }

  protected outcomeOf(row: TrailEvent): string {
    return signInOutcomeWording(signInOutcomeOf(row.action));
  }

  protected tone(row: TrailEvent): TrailTone {
    return trailTone(row.action);
  }

  /** "Chrome on Windows · 41.33.12.9": the browser and the address, whichever Identity kept. */
  protected place(row: TrailEvent): string {
    return [row.device, row.sourceAddress]
      .filter((part) => part !== null && part !== '')
      .join(' · ');
  }

  protected initials(row: TrailEvent): string {
    return initialsOf(row.userDisplayName ?? '');
  }

  protected methodName(method: SignInMethod): string {
    return signInMethodWording(method);
  }

  protected outcomeName(outcome: SignInOutcome): string {
    return signInOutcomeWording(outcome);
  }

  protected tenantName(choice: TenantChoice): string {
    return tenantChoiceName(choice, this.locale);
  }

  protected seen(device: MyDevice): string {
    return utcInstant(device.lastSeenUtc, this.locale);
  }

  /** The actions both filters allow: an outcome is a set, a method is a set, both together are their overlap. */
  private applyActions(): void {
    const outcome = this.outcome();
    const method = this.method();
    const byOutcome = outcome === '' ? null : this.actionsOfOutcome(outcome);
    const byMethod = method === '' ? null : actionsOfMethod(method);
    if (byOutcome === null && byMethod === null) {
      this.store.showActions([]);
      return;
    }
    const wanted = (byOutcome ?? byMethod ?? []).filter(
      (action) =>
        (byOutcome === null || byOutcome.includes(action)) &&
        (byMethod === null || byMethod.includes(action)),
    );
    // Two filters that share no action: a name nothing is written under, so the list is honestly empty.
    this.store.showActions(wanted.length === 0 ? ['sign-in.none'] : wanted);
  }

  private actionsOfOutcome(outcome: SignInOutcome): readonly string[] {
    return this.methods
      .flatMap((method) => actionsOfMethod(method))
      .concat([
        'sign-in.new-device-held',
        'sign-in.account-not-active',
        'sign-in.tenant-not-active',
      ])
      .filter((action) => signInOutcomeOf(action) === outcome);
  }

  private async loadCounts(): Promise<void> {
    const now = new Date();
    const [today, before] = await Promise.all([
      this.store.counts(daysAgo(ONE_DAY, now), null, null),
      this.store.counts(daysAgo(TWO_DAYS, now), daysAgo(ONE_DAY, now), null),
    ]);
    if (today.kind !== 'ok') {
      this.countsProblem.set(messageOf(today));
      return;
    }
    if (before.kind !== 'ok') {
      this.countsProblem.set(messageOf(before));
      return;
    }
    this.countsProblem.set(null);
    this.today.set(today.data);
    this.before.set(before.data);
  }
}
