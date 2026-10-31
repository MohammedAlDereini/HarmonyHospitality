import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { RolesStore, USERS_PAGE_SIZE, UsersStore } from '@harmony/administration/data-access';
import { AccountState, TwoStepFilter, UserAccount, roleName } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { Facet, FacetOption, Pager } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PopAt } from '@harmony/ui/shell';
import {
  AccountAction,
  AccountStateEditor,
  stateLabel,
  twoStepLabel,
} from '@harmony/administration/feature-users';
import { PeopleHead } from './people-head';
import { dayUtc, stampUtc } from './stamps';

/** The chip's tone beside a word (never colour alone). */
type ChipTone = 'ok' | 'warn' | 'stop' | 'vip' | null;

/** The account whose reason form is open in the drawer, and which write it makes. */
interface Editing {
  readonly user: UserAccount;
  readonly mode: AccountAction;
}

const drawerHeading: Readonly<Record<AccountAction, string>> = {
  suspend: $localize`:@@administration.users.drawer.suspend:Suspend account`,
  terminate: $localize`:@@administration.users.drawer.terminate:Terminate account`,
  exempt: $localize`:@@administration.users.drawer.exempt:Exempt from two-step`,
};
const doneToast: Readonly<Record<AccountAction, string>> = {
  suspend: $localize`:@@administration.users.toast.suspended:Account suspended.`,
  terminate: $localize`:@@administration.users.toast.terminated:Account terminated.`,
  exempt: $localize`:@@administration.users.toast.exempted:Exempted from two-step.`,
};
const stateTone: Readonly<Record<AccountState, ChipTone>> = {
  [AccountState.active]: 'ok',
  [AccountState.suspended]: 'warn',
  [AccountState.terminated]: 'stop',
};
const knownStates: readonly AccountState[] = [
  AccountState.active,
  AccountState.suspended,
  AccountState.terminated,
];
const twoStepChoices: readonly TwoStepFilter[] = ['on', 'off', 'exempt'];
/** What each Two-step answer looks like on an account, to word the facet with the chip's own words. */
const twoStepSample: Readonly<
  Record<TwoStepFilter, Pick<UserAccount, 'twoFactorEnabled' | 'twoStepExemptReason'>>
> = {
  on: { twoFactorEnabled: true, twoStepExemptReason: null },
  off: { twoFactorEnabled: false, twoStepExemptReason: null },
  exempt: { twoFactorEnabled: false, twoStepExemptReason: 'listed' },
};

/**
 * The platform's people (platform console design, C17): the head counts them, three facets narrow the list, each row
 * says who the person is, their roles, two-step, state, last sign-in and since when; the row's menu offers the doors
 * UserAccountController has (suspend, reinstate, terminate, exempt), each written in the drawer with a reason. Reads
 * through the shared store; every write carries its own intent key and reads the list again.
 */
@Component({
  selector: 'hr-people-page',
  imports: [
    RouterLink,
    IfAllowed,
    Icon,
    Facet,
    Pager,
    Drawer,
    PopAt,
    PeopleHead,
    AccountStateEditor,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './people-page.html',
  styleUrl: './people-page.css',
})
export class PeoplePage {
  /** ?role=: the roles matrix sends a person here to see who holds a role. */
  readonly role = input<string>();

  protected readonly store = inject(UsersStore);
  private readonly roles = inject(RolesStore);
  private readonly toasts = inject(Toasts);
  private readonly stateDrawer = viewChild.required<Drawer>('stateDrawer');
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly pageSize = USERS_PAGE_SIZE;
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly editing = signal<Editing | null>(null);
  protected readonly reinstating = signal<string | null>(null);
  protected readonly actionProblem = signal<string | null>(null);
  protected readonly stateHeading = computed(() => {
    const editing = this.editing();
    return editing === null ? '' : drawerHeading[editing.mode];
  });

  protected readonly stateOptions: readonly FacetOption[] = knownStates.map((state) => ({
    value: state,
    label: stateLabel({ state }),
  }));
  protected readonly twoStepOptions: readonly FacetOption[] = twoStepChoices.map((choice) => ({
    value: choice,
    label: twoStepLabel(twoStepSample[choice]),
  }));
  protected readonly roleOptions = computed<readonly FacetOption[]>(() =>
    this.roles.roles().map((role) => ({ value: role.id, label: roleName(role, this.locale) })),
  );
  protected readonly stateValue = computed<readonly string[]>(() => {
    const state = this.store.filters().state;
    return state === null ? [] : [state];
  });
  protected readonly twoStepValue = computed<readonly string[]>(() => {
    const twoStep = this.store.filters().twoStep;
    return twoStep === null ? [] : [twoStep];
  });
  protected readonly roleValue = computed<readonly string[]>(() => {
    const roleId = this.store.filters().roleId;
    return roleId === null ? [] : [roleId];
  });

  constructor() {
    // Back from a person's page: the list is read again, so a change made there shows here.
    if (this.store.rows().length > 0) this.store.reload();
    this.roles.load();
    effect(() => {
      const role = this.role();
      untracked(() => {
        if (role !== undefined) this.store.showRole(role);
      });
    });
  }

  protected rolesOf(user: UserAccount): string {
    return this.roles
      .roles()
      .filter((role) => user.roleIds.includes(role.id))
      .map((role) => roleName(role, this.locale))
      .join(' · ');
  }

  protected twoStepOf(user: UserAccount): string {
    return twoStepLabel(user);
  }

  protected twoStepTone(user: UserAccount): ChipTone {
    if (user.twoStepExemptReason !== null) return 'vip';
    return user.twoFactorEnabled ? 'ok' : 'warn';
  }

  protected stateOf(user: UserAccount): string {
    return stateLabel(user);
  }

  protected stateTone(user: UserAccount): ChipTone {
    return stateTone[user.state];
  }

  /** "Oct 29, 2026 09:12 UTC"; "never" for a person who has not signed in; '—' once terminated. */
  protected lastSignIn(user: UserAccount): string {
    if (user.state === AccountState.terminated) return '—';
    if (user.lastSignInUtc === null) return $localize`:@@administration.console.people.never:never`;
    return stampUtc(user.lastSignInUtc);
  }

  protected createdOn(user: UserAccount): string {
    return dayUtc(user.createdDate);
  }

  /** The company directory's people change state only there: no state action here. */
  protected canSuspend(user: UserAccount): boolean {
    return user.state === AccountState.active && !user.isDirectoryManaged;
  }

  protected canReinstate(user: UserAccount): boolean {
    return user.state === AccountState.suspended && !user.isDirectoryManaged;
  }

  /** Termination is final: never offered twice. */
  protected canTerminate(user: UserAccount): boolean {
    return user.state !== AccountState.terminated && !user.isDirectoryManaged;
  }

  protected canExempt(user: UserAccount): boolean {
    return user.twoStepExemptReason === null && user.state !== AccountState.terminated;
  }

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.store.search(text);
  }

  protected onState(values: readonly string[]): void {
    const chosen = values[0];
    this.store.showState(
      chosen !== undefined && (knownStates as readonly string[]).includes(chosen)
        ? (chosen as AccountState)
        : null,
    );
  }

  protected onTwoStep(values: readonly string[]): void {
    const chosen = values[0];
    this.store.showTwoStep(
      chosen !== undefined && (twoStepChoices as readonly string[]).includes(chosen)
        ? (chosen as TwoStepFilter)
        : null,
    );
  }

  protected onRole(values: readonly string[]): void {
    this.store.showRole(values[0] ?? null);
  }

  protected openState(user: UserAccount, mode: AccountAction): void {
    this.actionProblem.set(null);
    this.editing.set({ user, mode });
    this.stateDrawer().open();
  }

  protected stateDone(): void {
    const editing = this.editing();
    if (editing !== null) this.toasts.show(doneToast[editing.mode]);
    this.stateDrawer().close();
  }

  protected reinstate(user: UserAccount): void {
    void this.reinstateNow(user);
  }

  /** One intent key per press; the store reloads the list on success; a refusal is worded once. */
  private async reinstateNow(user: UserAccount): Promise<void> {
    this.actionProblem.set(null);
    this.reinstating.set(user.id);
    const result = await this.store.reinstate(user, newIntentKey());
    this.reinstating.set(null);
    if (result.kind === 'ok') {
      this.toasts.show($localize`:@@administration.users.toast.reinstated:Account reinstated.`);
      return;
    }
    this.actionProblem.set(messageOf(result));
  }
}
