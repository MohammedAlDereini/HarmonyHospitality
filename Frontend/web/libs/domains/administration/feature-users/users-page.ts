import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { USERS_PAGE_SIZE, UsersStore } from '@harmony/administration/data-access';
import { AccountState, UserAccount } from '@harmony/administration/domain';
import { AccessStore, IfAllowed, Permission } from '@harmony/platform/access';
import { InstantFormatter } from '@harmony/platform/context';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { Pager } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { Tone } from '@harmony/ui/primitives';
import { PageActionsTemplate, PageHead } from '@harmony/ui/shell';
import { AccountCreator } from './account-creator';
import { AccountAction, AccountStateEditor } from './account-state-editor';
import { stateLabel, toneOf, twoStepLabel } from './account-wording';

const dateOnly: Intl.DateTimeFormatOptions = { dateStyle: 'medium' };

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

/**
 * The accounts of the customer the way the platform app listed things: a lead, the search in a toolbar, a real
 * table, a pager; and, with ManageUsers, the writes in drawers: a new account from the context bar's action slot,
 * suspend or terminate with a reason, reinstate in place (architecture 6, administration). The buttons are
 * presentation: the API refuses on its own.
 */
@Component({
  selector: 'hr-users-page',
  imports: [
    RouterLink,
    IfAllowed,
    Icon,
    PageActionsTemplate,
    AccountCreator,
    AccountStateEditor,
    Pager,
    Drawer,
    PageHead,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-page.html',
  styleUrl: './users-page.css',
})
export class UsersPage {
  protected readonly store = inject(UsersStore);

  constructor() {
    // Back from a person's page: the list is read again, so a change made there shows here.
    if (this.store.rows().length > 0) this.store.reload();
  }
  private readonly access = inject(AccessStore);
  private readonly context = inject(InstantFormatter);
  private readonly toasts = inject(Toasts);
  private readonly creator = viewChild.required<Drawer>('creator');
  private readonly stateDrawer = viewChild.required<Drawer>('stateDrawer');

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly pageSize = USERS_PAGE_SIZE;
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly mayManage = computed(() => this.access.can(Permission.ManageUsers));

  /** True while the new-account form is open. */
  protected readonly creating = signal(false);
  /** The account whose suspension or termination is being written, if any. */
  protected readonly editing = signal<Editing | null>(null);
  /** The account being reinstated right now, if any. */
  protected readonly reinstating = signal<string | null>(null);
  /** One sentence when a reinstatement was refused; null while all is well. */
  protected readonly actionProblem = signal<string | null>(null);
  protected readonly stateHeading = computed(() => {
    const editing = this.editing();
    return editing === null ? '' : drawerHeading[editing.mode];
  });

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.store.search(text);
  }

  protected stateOf(user: UserAccount): string {
    return stateLabel(user);
  }

  protected toneOf(user: UserAccount): Tone | null {
    return toneOf(user);
  }

  /** The company directory's people change state only there (stage E): no state action here. */
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

  /** The write whose form is open for this row, or null. */
  protected editorFor(user: UserAccount): AccountAction | null {
    const editing = this.editing();
    return editing !== null && editing.user.id === user.id ? editing.mode : null;
  }

  protected twoStepOf(user: UserAccount): string {
    return twoStepLabel(user);
  }

  protected createdOn(user: UserAccount): string {
    return this.context.formatInstant(user.createdDate, dateOnly);
  }

  protected openCreator(): void {
    this.actionProblem.set(null);
    this.creating.set(true);
    this.creator().open();
  }

  protected created(): void {
    this.toasts.show(
      $localize`:@@administration.users.toast.added:Account added. The invitation is on its way.`,
    );
    this.creator().close();
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
