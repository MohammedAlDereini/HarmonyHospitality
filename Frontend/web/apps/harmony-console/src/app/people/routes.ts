import { Routes } from '@angular/router';
import { InvitationsStore } from '@harmony/administration/data-access';
import { CompanyLoginConsolePage } from './company-login-console-page';
import { DirectoryConsolePage } from './directory-console-page';
import { InvitationsConsolePage } from './invitations-console-page';
import { PeoplePage } from './people-page';
import { PersonConsolePage } from './person-console-page';
import { RulesConsolePage } from './rules-console-page';

/**
 * The console's People (platform console design, C17 to C22), mounted at /people behind allowed(Permission.ViewUsers).
 * One invitations store serves every tab, so the head counts what waits wherever the person is.
 */
export const consoleRoutes: Routes = [
  {
    path: '',
    providers: [InvitationsStore],
    children: [
      {
        path: '',
        component: PeoplePage,
        title: $localize`:@@administration.console.people.page-title:People`,
      },
      {
        path: 'invitations',
        component: InvitationsConsolePage,
        title: $localize`:@@administration.invitations.page-title:Invitations`,
      },
      {
        path: 'company-login',
        component: CompanyLoginConsolePage,
        title: $localize`:@@administration.company-login.page-title:Company login`,
      },
      {
        path: 'directory',
        component: DirectoryConsolePage,
        title: $localize`:@@administration.directory.page-title:Company directory`,
      },
      {
        path: 'security-rules',
        component: RulesConsolePage,
        title: $localize`:@@administration.rules.page-title:Approvals and rules`,
      },
      {
        path: 'account/:id',
        component: PersonConsolePage,
        title: $localize`:@@administration.person.page-title:Person`,
        data: { crumb: $localize`:@@administration.console.people.crumb:People` },
      },
    ],
  },
];
