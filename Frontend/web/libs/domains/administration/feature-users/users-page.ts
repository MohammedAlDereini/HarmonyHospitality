import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { USERS_PAGE_SIZE, UsersStore } from '@harmony/administration/data-access';
import { AccountState, UserAccount } from '@harmony/administration/domain';
import { PropertyContext } from '@harmony/platform/context';
import { initialsOf } from '@harmony/shared/domain';
import { Tone } from '@harmony/ui/primitives';

const PAGE_STEP = 1;
const dateOnly: Intl.DateTimeFormatOptions = { dateStyle: 'medium' };

/** The accounts of the customer: who may sign in, how they sign in, since when (architecture 6, administration). */
@Component({
  selector: 'hr-users-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-page.html',
  styleUrl: './users-page.css',
})
export class UsersPage {
  protected readonly store = inject(UsersStore);
  private readonly context = inject(PropertyContext);

  protected readonly page = computed(() => this.store.filters().page);
  protected readonly from = computed(() => (this.page() - PAGE_STEP) * USERS_PAGE_SIZE + PAGE_STEP);
  protected readonly to = computed(() =>
    Math.min(this.page() * USERS_PAGE_SIZE, this.store.total()),
  );
  protected readonly hasPrevious = computed(() => this.page() > PAGE_STEP);
  protected readonly hasNext = computed(() => this.to() < this.store.total());

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
}
