import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { InvitationsStore, RolesStore, UsersStore } from '@harmony/administration/data-access';
import { UserAccount, roleName } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { csvFile } from '@harmony/shared/domain';
import { saveTextFile } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead } from '@harmony/ui/shell';
import { AccountCreator, stateLabel, twoStepLabel } from '@harmony/administration/feature-users';
import { PeopleTab, PeopleTabs } from './people-tabs';
import { dayUtc, stampUtc } from './stamps';

/**
 * The head every People page shares (platform console design, C17): the title, the census of the platform's
 * accounts and the invitations waiting, Export and New person, then the tabs. New person opens the account creator in
 * the drawer; Export writes the people on hand as CSV.
 */
@Component({
  selector: 'hr-people-head',
  imports: [IfAllowed, Icon, PageHead, Drawer, AccountCreator, PeopleTabs],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './people-head.html',
})
export class PeopleHead {
  readonly current = input.required<PeopleTab>();

  protected readonly users = inject(UsersStore);
  private readonly invitations = inject(InvitationsStore);
  private readonly roles = inject(RolesStore);
  private readonly toasts = inject(Toasts);
  private readonly document = inject(DOCUMENT);
  private readonly creator = viewChild.required<Drawer>('creator');
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly creating = signal(false);
  protected readonly census = computed(() => this.users.census());
  protected readonly waiting = computed(() => this.invitations.rows().length);

  constructor() {
    this.users.countAll();
    this.roles.load();
  }

  protected openCreator(): void {
    this.creating.set(true);
    this.creator().open();
  }

  protected created(): void {
    this.toasts.show(
      $localize`:@@administration.users.toast.added:Account added. The invitation is on its way.`,
    );
    this.creator().close();
    this.invitations.reload();
  }

  protected exportCsv(): void {
    const rows = this.users.rows();
    if (rows.length === 0) return;
    const header = ['Name', 'E-mail', 'Roles', 'Two-step', 'State', 'Last sign-in', 'Created'];
    const lines = rows.map((user) => [
      user.displayName,
      user.email ?? user.userName,
      this.rolesOf(user),
      twoStepLabel(user),
      stateLabel(user),
      user.lastSignInUtc === null ? '' : stampUtc(user.lastSignInUtc),
      dayUtc(user.createdDate),
    ]);
    saveTextFile(
      this.document,
      'harmony-people.csv',
      csvFile(header, lines),
      'text/csv;charset=utf-8',
    );
    this.toasts.show(
      $localize`:@@administration.console.people.exported:${rows.length}:count: people exported.`,
    );
  }

  private rolesOf(user: UserAccount): string {
    return this.roles
      .roles()
      .filter((role) => user.roleIds.includes(role.id))
      .map((role) => roleName(role, this.locale))
      .join(' · ');
  }
}
