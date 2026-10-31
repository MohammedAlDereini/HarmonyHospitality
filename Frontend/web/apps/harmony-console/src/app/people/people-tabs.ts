import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { InvitationsStore, UsersStore } from '@harmony/administration/data-access';

/** The five places under People in the console (C17, C19, C20, C21, C22). */
export type PeopleTab = 'people' | 'invitations' | 'company-login' | 'directory' | 'rules';

/**
 * The tabs under the People head (platform console design, C17): each is a page of its own, so they are links; the
 * two counted ones say how many people and how many invitations wait.
 */
@Component({
  selector: 'hr-people-tabs',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'c-tabs', role: 'tablist' },
  template: `
    <a routerLink="/people" [attr.aria-current]="current() === 'people' ? 'page' : null"
      ><ng-container i18n="@@administration.console.people.tab.people">People</ng-container
      >&ngsp;<span class="n">· {{ people() }}</span></a
    >
    <a
      routerLink="/people/invitations"
      [attr.aria-current]="current() === 'invitations' ? 'page' : null"
      ><ng-container i18n="@@administration.console.people.tab.invitations"
        >Invitations</ng-container
      >&ngsp;<span class="n">· {{ waiting() }}</span></a
    >
    <a
      routerLink="/people/company-login"
      [attr.aria-current]="current() === 'company-login' ? 'page' : null"
      i18n="@@administration.console.people.tab.company-login"
      >Company login</a
    >
    <a
      routerLink="/people/directory"
      [attr.aria-current]="current() === 'directory' ? 'page' : null"
      i18n="@@administration.console.people.tab.directory"
      >Company directory</a
    >
    <a
      routerLink="/people/security-rules"
      [attr.aria-current]="current() === 'rules' ? 'page' : null"
      i18n="@@administration.console.people.tab.rules"
      >Approvals and rules</a
    >
  `,
})
export class PeopleTabs {
  readonly current = input.required<PeopleTab>();

  private readonly users = inject(UsersStore);
  private readonly invitations = inject(InvitationsStore);

  /** Every platform account, from the census when it was read; the list's own total until then. */
  protected readonly people = computed(() => this.users.census()?.total ?? this.users.total());
  protected readonly waiting = computed(() => this.invitations.rows().length);
}
