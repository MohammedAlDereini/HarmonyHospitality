import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { USERS_PAGE_SIZE, UsersStore } from '@harmony/administration/data-access';
import { AccountState, UserAccount } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { PropertyContext } from '@harmony/platform/context';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { initialsOf } from '@harmony/shared/domain';
import { Icon } from '@harmony/ui/icons';
import { Tone } from '@harmony/ui/primitives';
import { PageActionsTemplate } from '@harmony/ui/shell';
import { AccountCreator } from './account-creator';
import { AccountStateEditor } from './account-state-editor';

const PAGE_STEP = 1;
const dateOnly: Intl.DateTimeFormatOptions = { dateStyle: 'medium' };

/**
 * The accounts of the customer: who may sign in, how they sign in, since when; and, with ManageUsers, the writes:
 * a new account from the context bar's action slot, suspend with a reason, reinstate (architecture 6,
 * administration). The buttons are presentation: the API refuses on its own.
 */
@Component({
  selector: 'hr-users-page',
  imports: [IfAllowed, Icon, PageActionsTemplate, AccountCreator, AccountStateEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-page.html',
  styleUrl: './users-page.css',
})
export class UsersPage {
  protected readonly store = inject(UsersStore);
  private readonly context = inject(PropertyContext);

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
  /** The account whose suspension is being written, if any. */
  protected readonly editing = signal<string | null>(null);
  /** The account being reinstated right now, if any. */
  protected readonly reinstating = signal<string | null>(null);
  /** One sentence when a reinstatement was refused; null while all is well. */
  protected readonly actionProblem = signal<string | null>(null);

  private readonly stateWording: Readonly<Record<AccountState, string>> = {
    [AccountState.active]: $localize`:@@administration.users.state.active:Active`,
    [AccountState.suspended]: $localize`:@@administration.users.state.suspended:Suspended`,
    [AccountState.terminated]: $localize`:@@administration.users.state.terminated:Terminated`,
  };

  /** Text says the state; the tone only underlines it (status never by colour alone). */
  private readonly stateTone: Readonly<Record<AccountState, Tone | null>> = {
    [AccountState.active]: null,
    [AccountState.suspended]: 'warn',
    [AccountState.terminated]: 'stop',
  };

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
    return this.stateWording[user.state];
  }

  protected toneOf(user: UserAccount): Tone | null {
    return this.stateTone[user.state];
  }

  protected canSuspend(user: UserAccount): boolean {
    return user.state === AccountState.active;
  }

  protected canReinstate(user: UserAccount): boolean {
    return user.state === AccountState.suspended;
  }

  protected twoStepOf(user: UserAccount): string {
    if (user.twoStepExemptReason !== null)
      return $localize`:@@administration.users.two-step.exempt:Exempt`;
    return user.twoFactorEnabled
      ? $localize`:@@administration.users.two-step.on:On`
      : $localize`:@@administration.users.two-step.off:Not set`;
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
    this.editing.set(user.id);
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
