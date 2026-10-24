import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormField, email, form, required, submit } from '@angular/forms/signals';
import { PropertiesStore, TenantsStore } from '@harmony/platform-admin/data-access';
import { SubscriptionLine, Tenant, TenantStatus, tenantName } from '@harmony/platform-admin/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { Tone } from '@harmony/ui/primitives';
import { ReasonForm } from './reason-form';
import { SubscriptionEditor } from './subscription-editor';
import { TenantEditor } from './tenant-editor';
import { statusLabel, statusToneOf } from './tenant-wording';

/** What is open on the page: one thing at a time. */
type Opened = 'edit' | 'suspend' | 'invite' | 'subscribe';

const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/**
 * One customer: who they are, their status with suspend and reinstate, the administrator's invitation, and the
 * subscription lines with subscribe and unsubscribe (architecture 6, platform-admin). Every write is one press
 * with its own intent key; the page reads the tenant again afterwards.
 */
@Component({
  selector: 'hr-tenant-page',
  imports: [RouterLink, FormField, IfAllowed, TenantEditor, ReasonForm, SubscriptionEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tenant-page.html',
  styleUrl: './tenant-page.css',
})
export class TenantPage {
  /** From the route (:id), bound by the router. */
  readonly id = input.required<string>();

  private readonly store = inject(TenantsStore);
  private readonly properties = inject(PropertiesStore);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly tenant = signal<Tenant | null>(null);
  protected readonly loading = signal(true);
  protected readonly problem = signal<string | null>(null);
  protected readonly opened = signal<Opened | null>(null);
  /** The line whose unsubscribe reason is being typed, if any. */
  protected readonly ending = signal<string | null>(null);
  protected readonly busy = signal(false);
  /** One sentence when a press was refused; null while all is well. */
  protected readonly actionProblem = signal<string | null>(null);
  /** Said once after the invitation went out. */
  protected readonly invited = signal(false);

  private readonly inviteModel = signal({ administratorEmail: '', administratorDisplayName: '' });
  protected readonly inviteForm = form(this.inviteModel, (path) => {
    required(path.administratorEmail, { message: wording('10516') });
    email(path.administratorEmail, { message: wording('10517') });
    required(path.administratorDisplayName, { message: wording('10519') });
  });

  protected readonly suspendLabel = $localize`:@@platform-admin.tenants.suspend.reason:Reason for the suspension`;
  protected readonly suspendPlaceholder = $localize`:@@platform-admin.tenants.suspend.placeholder:Why this tenant is being suspended`;
  protected readonly suspendGo = $localize`:@@platform-admin.tenants.suspend.go:Suspend tenant`;
  protected readonly unsubscribeLabel = $localize`:@@platform-admin.subscriptions.end.reason:Reason for ending the subscription`;
  protected readonly unsubscribePlaceholder = $localize`:@@platform-admin.subscriptions.end.placeholder:Why this module is being taken away`;
  protected readonly unsubscribeGo = $localize`:@@platform-admin.subscriptions.end.go:End subscription`;

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => {
        this.properties.ofTenant(id);
        void this.read(id);
      });
    });
  }

  protected name(tenant: Tenant): string {
    return tenantName(tenant, this.locale);
  }

  protected status(tenant: Tenant): string {
    return statusLabel(tenant);
  }

  protected tone(tenant: Tenant): Tone | null {
    return statusToneOf(tenant);
  }

  protected isActive(tenant: Tenant): boolean {
    return tenant.status === TenantStatus.active;
  }

  protected isSuspended(tenant: Tenant): boolean {
    return tenant.status === TenantStatus.suspended;
  }

  protected scopeOf(line: SubscriptionLine): string {
    return line.propertyId === null
      ? $localize`:@@platform-admin.subscriptions.scope.whole:Whole tenant`
      : $localize`:@@platform-admin.subscriptions.scope.property:One property`;
  }

  protected lineState(line: SubscriptionLine): string {
    if (line.isHeld) return $localize`:@@platform-admin.subscriptions.state.held:Held`;
    return line.isEnabled
      ? $localize`:@@platform-admin.subscriptions.state.enabled:Enabled`
      : $localize`:@@platform-admin.subscriptions.state.disabled:Disabled`;
  }

  protected reload(): void {
    void this.read(this.id());
  }

  protected open(what: Opened): void {
    this.actionProblem.set(null);
    this.invited.set(false);
    this.ending.set(null);
    this.opened.set(this.opened() === what ? null : what);
  }

  protected close(): void {
    this.opened.set(null);
  }

  protected changed(): void {
    this.opened.set(null);
    this.reload();
  }

  protected askEnd(line: SubscriptionLine): void {
    this.actionProblem.set(null);
    this.opened.set(null);
    this.ending.set(this.ending() === line.id ? null : line.id);
  }

  protected suspend(tenant: Tenant, reason: string): void {
    void this.press(this.store.suspend(tenant, reason, newIntentKey()), () => {
      this.opened.set(null);
    });
  }

  protected reinstate(tenant: Tenant): void {
    void this.press(this.store.reinstate(tenant, newIntentKey()));
  }

  protected endLine(tenant: Tenant, line: SubscriptionLine, reason: string): void {
    void this.press(this.store.unsubscribe(tenant, line, reason, newIntentKey()), () => {
      this.ending.set(null);
    });
  }

  protected onInvite(event: Event, tenant: Tenant): void {
    event.preventDefault();
    void submit(this.inviteForm, async (field) => {
      const value = field().value();
      await this.press(
        this.store.invite(
          tenant,
          value.administratorEmail.trim(),
          value.administratorDisplayName.trim(),
          newIntentKey(),
        ),
        () => {
          this.opened.set(null);
          this.invited.set(true);
        },
      );
      return undefined;
    });
  }

  private async read(id: string): Promise<void> {
    this.loading.set(true);
    const result = await this.store.detail(id);
    if (result.kind === 'ok') {
      this.tenant.set(result.data);
      this.problem.set(null);
    } else {
      this.problem.set(messageOf(result));
    }
    this.loading.set(false);
  }

  /** One press: nothing else until it settles; success reads the tenant again; a refusal is worded once. */
  private async press(call: Promise<ApiResult<unknown>>, after?: () => void): Promise<void> {
    this.actionProblem.set(null);
    this.busy.set(true);
    const result = await call;
    this.busy.set(false);
    if (result.kind === 'ok') {
      after?.();
      this.reload();
      return;
    }
    this.actionProblem.set(messageOf(result));
  }
}
