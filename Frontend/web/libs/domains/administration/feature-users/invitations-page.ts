import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import { InvitationsStore } from '@harmony/administration/data-access';
import { Invitation } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { InstantFormatter } from '@harmony/platform/context';
import { messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { ConfirmDialog, Toasts } from '@harmony/ui/overlays';
import { PageHead } from '@harmony/ui/shell';

const dateAndTime: Intl.DateTimeFormatOptions = { dateStyle: 'medium', timeStyle: 'short' };

/**
 * The invitations still waiting for a first password (stage B). A link lives 24 hours. Resend mails a new link and kills
 * the old one; Cancel kills it for good (asked first). One intent key per press; every refusal worded once.
 */
@Component({
  selector: 'hr-invitations-page',
  imports: [IfAllowed, ConfirmDialog, PageHead],
  providers: [InvitationsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './invitations-page.html',
  styleUrl: './invitations-page.css',
})
export class InvitationsPage {
  protected readonly store = inject(InvitationsStore);
  private readonly toasts = inject(Toasts);
  private readonly context = inject(InstantFormatter);
  private readonly cancelDialog = viewChild.required<ConfirmDialog>('cancelDialog');

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);

  protected when(instant: string): string {
    return this.context.formatInstant(instant, dateAndTime);
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
