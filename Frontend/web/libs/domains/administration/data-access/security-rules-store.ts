import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { ApprovalRequest, CustomerRules } from '@harmony/administration/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';

export const myTenantPath = '/api/Tenant/mine';
export const pendingApprovalsPath = '/api/Approval/pending';
export const approvePath = '/api/Approval/Approve';
export const rejectPath = '/api/Approval/Reject';
export const approvalsRequiredPath = '/api/Approval/Required';
export const inactivityDaysPath = '/api/Directory/InactivityDays';
export const trustedDevicesPath = '/api/UserAccount/TrustedDevices';

/**
 * The customer's security rules (stages D and E): approvals on or off with the changes waiting for a second
 * administrator, and the days without a sign-in before an account is suspended. Provided by the page; every write that
 * succeeded reads again (a request approved here may have changed the rules themselves).
 */
@Injectable()
export class SecurityRulesStore {
  private readonly api = inject(ApiClient);
  private readonly tenant = httpResource<Envelope<CustomerRules>>(() => ({ url: myTenantPath }));
  private readonly pending = httpResource<Envelope<ApprovalRequest[]>>(() => ({
    url: pendingApprovalsPath,
  }));

  readonly rules = computed<CustomerRules | null>(() => this.tenant.value()?.data ?? null);
  readonly waiting = computed<readonly ApprovalRequest[]>(() => this.pending.value()?.data ?? []);
  readonly loading = computed(() => this.tenant.isLoading() || this.pending.isLoading());
  readonly problem = computed(() => {
    const error = this.tenant.error() ?? this.pending.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  reload(): void {
    this.tenant.reload();
    this.pending.reload();
  }

  approve(request: ApprovalRequest, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(approvePath, { id: request.id }, intent);
  }

  reject(request: ApprovalRequest, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(rejectPath, { id: request.id }, intent);
  }

  /** Off waits for a second administrator while approvals are on (00131). */
  setApprovals(required: boolean, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(approvalsRequiredPath, { required }, intent);
  }

  setInactivityDays(days: number, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(inactivityDaysPath, { days }, intent);
  }

  /** The new-device gate on or off: a sign-in from an untrusted browser waits for the person's e-mail click. */
  setTrustedDevices(required: boolean, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(trustedDevicesPath, { required }, intent);
  }

  private async write(path: string, body: unknown, intent: IntentKey): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(this.api.put(path, body, intent));
    if (result.kind === 'ok') this.reload();
    return result;
  }
}
