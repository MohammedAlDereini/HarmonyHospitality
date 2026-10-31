import { ChangeDetectionStrategy, Component, inject, linkedSignal, signal } from '@angular/core';
import { FormField, form, max, min, required, submit } from '@angular/forms/signals';
import { SecurityRulesStore } from '@harmony/administration/data-access';
import {
  ApprovalRequest,
  MAX_INACTIVITY_DAYS,
  approvalKindName,
} from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { InstantFormatter } from '@harmony/platform/context';
import { IdentityConfirmation } from '@harmony/platform/session';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { Toasts } from '@harmony/ui/overlays';
import { PageHead } from '@harmony/ui/shell';
import { sentForApproval, sentForApprovalToast } from './pending-approval';

const dateAndTime: Intl.DateTimeFormatOptions = { dateStyle: 'medium', timeStyle: 'short' };
const FEWEST_DAYS = 1;

const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

interface DaysDraft {
  readonly days: number | null;
}

/**
 * The customer's security rules (stages D and E). Approvals: when on, risky changes (an administrator role, a two-step
 * exception, a merge, company sign-in off) wait here for a second administrator, who approves or rejects them; the
 * person who asked never decides. Inactive accounts: the days without a sign-in before a nightly job suspends an account
 * (90 at most, PCI DSS 8.2.6). One intent key per press; every refusal worded once.
 */
@Component({
  selector: 'hr-security-rules-page',
  imports: [FormField, IfAllowed, PageHead],
  providers: [SecurityRulesStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './security-rules-page.html',
  styleUrl: './security-rules-page.css',
})
export class SecurityRulesPage {
  protected readonly store = inject(SecurityRulesStore);
  private readonly toasts = inject(Toasts);
  private readonly context = inject(InstantFormatter);
  private readonly confirmation = inject(IdentityConfirmation);

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);

  private readonly model = linkedSignal<DaysDraft>(() => ({
    days: this.store.rules()?.inactivityDays ?? null,
  }));
  protected readonly form = form(this.model, (path) => {
    required(path.days, { message: wording('11104') });
    min(path.days, FEWEST_DAYS, { message: wording('11104') });
    max(path.days, MAX_INACTIVITY_DAYS, { message: wording('11104') });
  });

  protected kindOf(request: ApprovalRequest): string {
    return approvalKindName(request.kind);
  }

  protected when(instant: string): string {
    return this.context.formatInstant(instant, dateAndTime);
  }

  protected async switchApprovals(on: boolean): Promise<void> {
    const result = await this.run(
      this.confirmation.run((intent) => this.store.setApprovals(on, intent)),
    );
    if (result.kind === 'ok') {
      this.toasts.show(
        on
          ? $localize`:@@administration.rules.toast.approvals-on:Approvals are on.`
          : $localize`:@@administration.rules.toast.approvals-off:Approvals are off.`,
      );
    } else if (sentForApproval(result)) {
      this.toasts.show(sentForApprovalToast());
      this.store.reload();
    } else {
      this.problem.set(messageOf(result));
    }
  }

  protected async switchTrustedDevices(on: boolean): Promise<void> {
    const result = await this.run(this.store.setTrustedDevices(on, newIntentKey()));
    if (result.kind === 'ok')
      this.toasts.show(
        on
          ? $localize`:@@administration.rules.toast.devices-on:New browsers must be trusted first.`
          : $localize`:@@administration.rules.toast.devices-off:New browsers sign in without a trust step.`,
      );
    else this.problem.set(messageOf(result));
  }

  protected async approve(request: ApprovalRequest): Promise<void> {
    const result = await this.run(this.store.approve(request, newIntentKey()));
    if (result.kind === 'ok')
      this.toasts.show(
        $localize`:@@administration.rules.toast.approved:Approved: the change is done.`,
      );
    else this.problem.set(messageOf(result));
  }

  protected async reject(request: ApprovalRequest): Promise<void> {
    const result = await this.run(this.store.reject(request, newIntentKey()));
    if (result.kind === 'ok')
      this.toasts.show(
        $localize`:@@administration.rules.toast.rejected:Rejected: nothing changed.`,
      );
    else this.problem.set(messageOf(result));
  }

  protected async saveDays(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.form, async () => {
      const days = this.model().days;
      if (days === null) return;
      const result = await this.run(this.store.setInactivityDays(days, newIntentKey()));
      if (result.kind === 'ok')
        this.toasts.show($localize`:@@administration.rules.toast.days-saved:Saved.`);
      else this.problem.set(messageOf(result));
    });
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
