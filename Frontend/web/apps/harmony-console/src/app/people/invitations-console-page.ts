import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { InvitationsStore, RolesStore, UsersStore } from '@harmony/administration/data-access';
import { Invitation, roleName } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { Facet, FacetOption } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, Toasts } from '@harmony/ui/overlays';
import { PopAt } from '@harmony/ui/shell';
import { PeopleHead } from './people-head';
import { stampUtc } from './stamps';

/** The Link facet's two answers. */
type LinkFilter = 'valid' | 'expired';
const VALID: LinkFilter = 'valid';
const EXPIRED: LinkFilter = 'expired';

/**
 * The platform's invitations still waiting for a first password (platform console design, C19, a tab of People): who
 * was invited, by whom, when, whether the link still works, and the role the account holds on arrival. Send again
 * mails a new link and kills the old one; Cancel kills it for good (asked first). One intent key per press.
 */
@Component({
  selector: 'hr-invitations-console-page',
  imports: [IfAllowed, Icon, Facet, ConfirmDialog, PopAt, PeopleHead],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './invitations-console-page.html',
  styleUrl: './invitations-console-page.css',
})
export class InvitationsConsolePage {
  protected readonly store = inject(InvitationsStore);
  private readonly users = inject(UsersStore);
  private readonly roles = inject(RolesStore);
  private readonly toasts = inject(Toasts);
  private readonly cancelDialog = viewChild.required<ConfirmDialog>('cancelDialog');
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);
  protected readonly search = signal('');
  protected readonly link = signal<LinkFilter | null>(null);
  protected readonly linkOptions: readonly FacetOption[] = [
    {
      value: VALID,
      label: $localize`:@@administration.console.invitations.link.valid:Still valid`,
    },
    {
      value: EXPIRED,
      label: $localize`:@@administration.console.invitations.link.expired:Expired`,
    },
  ];
  protected readonly linkValue = computed<readonly string[]>(() => {
    const link = this.link();
    return link === null ? [] : [link];
  });
  /** The invitations as filtered on the page itself; Identity sends the whole list. */
  protected readonly rows = computed<readonly Invitation[]>(() => {
    const needle = this.search().trim().toLowerCase();
    const link = this.link();
    return this.store.rows().filter((invitation) => {
      if (link === VALID && invitation.isExpired) return false;
      if (link === EXPIRED && !invitation.isExpired) return false;
      if (needle === '') return true;
      return (
        invitation.displayName.toLowerCase().includes(needle) ||
        (invitation.email ?? '').toLowerCase().includes(needle)
      );
    });
  });

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.search.set(text);
  }

  protected onLink(values: readonly string[]): void {
    const chosen = values[0];
    this.link.set(chosen === VALID || chosen === EXPIRED ? chosen : null);
  }

  protected stamp(instant: string): string {
    return stampUtc(instant);
  }

  protected linkWord(invitation: Invitation): string {
    const until = stampUtc(invitation.expiresAtUtc);
    return invitation.isExpired
      ? $localize`:@@administration.console.invitations.expired-on:Expired ${until}:when:`
      : $localize`:@@administration.console.invitations.valid-until:valid until ${until}:when:`;
  }

  /** The roles the account holds today: what the person gets on arrival. */
  protected roleOnArrival(invitation: Invitation): string {
    const account =
      this.users.everyone().find((row) => row.id === invitation.userAccountId) ??
      this.users.rows().find((row) => row.id === invitation.userAccountId);
    if (account === undefined) return '—';
    return this.roles
      .roles()
      .filter((role) => account.roleIds.includes(role.id))
      .map((role) => roleName(role, this.locale))
      .join(', ');
  }

  protected async resend(invitation: Invitation): Promise<void> {
    const result = await this.run(this.store.resend(invitation, newIntentKey()));
    if (result.kind === 'ok')
      this.toasts.show(
        $localize`:@@administration.invitations.toast.resent:A new link is on its way; the old one no longer works.`,
      );
    else this.problem.set(messageOf(result));
  }

  protected async cancel(invitation: Invitation): Promise<void> {
    if (!(await this.cancelDialog().ask())) return;
    const result = await this.run(this.store.cancel(invitation, newIntentKey()));
    if (result.kind === 'ok')
      this.toasts.show(
        $localize`:@@administration.invitations.toast.cancelled:Invitation cancelled.`,
      );
    else this.problem.set(messageOf(result));
  }

  /** One press: nothing else until it settles. */
  private async run(call: Promise<ApiResult<unknown>>): Promise<ApiResult<unknown>> {
    this.problem.set(null);
    this.busy.set(true);
    const result = await call;
    this.busy.set(false);
    return result;
  }
}
