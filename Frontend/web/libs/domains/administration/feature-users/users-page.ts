import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { USERS_PAGE_SIZE, UsersStore } from '@harmony/administration/data-access';
import { AccountState, UserAccount } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { InstantFormatter } from '@harmony/platform/context';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { initialsOf } from '@harmony/shared/domain';
import { Icon } from '@harmony/ui/icons';
import { Tone } from '@harmony/ui/primitives';
import { PageActionsTemplate } from '@harmony/ui/shell';
import { AccountCreator } from './account-creator';
import { AccountAction, AccountStateEditor } from './account-state-editor';
import { stateLabel, toneOf, twoStepLabel } from './account-wording';

const PAGE_STEP = 1;
const dateOnly: Intl.DateTimeFormatOptions = { dateStyle: 'medium' };

/** The row whose reason form is open, and which write it makes. */
interface Editing {
  readonly id: string;
  readonly mode: AccountAction;
}

/**
 * The accounts of the customer: who may sign in, how they sign in, since when; and, with ManageUsers, the writes:
 * a new account from the context bar's action slot, suspend with a reason, reinstate, terminate (architecture 6,
 * administration). The buttons are presentation: the API refuses on its own.
 */
@Component({
  selector: 'hr-users-page',
  imports: [RouterLink, IfAllowed, Icon, PageActionsTemplate, AccountCreator, AccountStateEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-page.html',
  styleUrl: './users-page.css',
})
export class UsersPage {
  protected readonly store = inject(UsersStore);
  private readonly context = inject(InstantFormatter);

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly from = computed(() => (this.page() - PAGE_STEP) * USERS_PAGE_SIZE + PAGE_STEP);
  protected readonly to = computed(() =>
    Math.min(this.page() * USERS_PAGE_SIZE, this.store.total()),
  );
  protected readonly hasPrevious = computed(() => this.page() > PAGE_STEP);
  protected readonly hasNext = computed(() => this.to() < this.store.total());

  /** True while the new-account form is open. */
  protected readonly creating = signal(false);
  /** The account whose suspension or termination is being written, if any. */
  protected readonly editing = signal<Editing | null>(null);
  /** The account being reinstated right now, if any. */
  protected readonly reinstating = signal<string | null>(null);
  /** One sentence when a reinstatement was refused; null while all is well. */
  protected readonly actionProblem = signal<string | null>(null);

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

  protected initials(user: UserAccount): string {
    return initialsOf(user.displayName);
  }

  protected stateOf(user: UserAccount): string {
    return stateLabel(user);
  }

  protected toneOf(user: UserAccount): Tone | null {
    return toneOf(user);
  }

  protected canSuspend(user: UserAccount): boolean {
    return user.state === AccountState.active;
  }

  protected canReinstate(user: UserAccount): boolean {
    return user.state === AccountState.suspended;
  }

  /** Termination is final: never offered twice. */
  protected canTerminate(user: UserAccount): boolean {
    return user.state !== AccountState.terminated;
  }

  /** The write whose form is open under this row, or null. */
  protected editorFor(user: UserAccount): AccountAction | null {
    const editing = this.editing();
    return editing !== null && editing.id === user.id ? editing.mode : null;
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
  }

  protected closeCreator(): void {
    this.creating.set(false);
  }

  protected openSuspend(user: UserAccount): void {
    this.actionProblem.set(null);
    this.editing.set({ id: user.id, mode: 'suspend' });
  }

  protected openTerminate(user: UserAccount): void {
    this.actionProblem.set(null);
    this.editing.set({ id: user.id, mode: 'terminate' });
  }

  protected closeEditor(): void {
    this.editing.set(null);
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
    if (result.kind !== 'ok') this.actionProblem.set(messageOf(result));
  }
}
