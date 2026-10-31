import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  linkedSignal,
  signal,
} from '@angular/core';
import { FormField, form, max, min, required, submit } from '@angular/forms/signals';
import { SecurityRulesStore } from '@harmony/administration/data-access';
import {
  ApprovalRequest,
  MAX_INACTIVITY_DAYS,
  approvalKindName,
} from '@harmony/administration/domain';
import { AccessStore, IfAllowed, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { IdentityConfirmation } from '@harmony/platform/session';
import { Icon } from '@harmony/ui/icons';
import { Toasts } from '@harmony/ui/overlays';
import { sentForApproval, sentForApprovalToast } from '@harmony/administration/feature-users';
import { PeopleHead } from './people-head';
import { stampUtc } from './stamps';

const FEWEST_DAYS = 1;
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

interface DaysDraft {
  readonly days: number | null;
}

/**
 * The platform's own security rules (platform console design, C20, a tab of People): approvals by a second
 * administrator, the trust step for new browsers, the days before an unused account is suspended; what waits for
 * approval, approved or rejected here; and what Identity enforces on its own, said so. One intent key per press;
 * every refusal worded once.
 */
@Component({
  selector: 'hr-rules-console-page',
  imports: [FormField, IfAllowed, Icon, PeopleHead],
  providers: [SecurityRulesStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './rules-console-page.html',
  styleUrl: './rules-console-page.css',
})
export class RulesConsolePage {
  protected readonly store = inject(SecurityRulesStore);
  private readonly toasts = inject(Toasts);
  private readonly confirmation = inject(IdentityConfirmation);
  private readonly access = inject(AccessStore);

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly mayManage = computed(() => this.access.can(Permission.ManageUsers));
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

  protected stamp(instant: string): string {
    return stampUtc(instant);
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
      this.store.reload();
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
    else {
      this.problem.set(messageOf(result));
      this.store.reload();
    }
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
