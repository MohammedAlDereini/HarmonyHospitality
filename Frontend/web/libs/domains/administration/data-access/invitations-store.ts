import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { Invitation } from '@harmony/administration/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';

export const myInvitationsPath = '/api/Invitation/mine';
export const resendInvitationPath = '/api/Invitation/Resend';
export const cancelInvitationPath = '/api/Invitation/Cancel';

/**
 * The customer's invitations still waiting for a first password (stage B). Resend mails a new link and kills the old
 * one; Cancel kills it for good. Both name the account. Provided by the page; every write that succeeded reads again.
 */
@Injectable()
export class InvitationsStore {
  private readonly api = inject(ApiClient);
  private readonly read = httpResource<Envelope<Invitation[]>>(() => ({ url: myInvitationsPath }));

  readonly rows = computed<readonly Invitation[]>(() => this.read.value()?.data ?? []);
  readonly loading = this.read.isLoading;
  readonly problem = computed(() => {
    const error = this.read.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  reload(): void {
    this.read.reload();
  }

  resend(invitation: Invitation, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(resendInvitationPath, { id: invitation.userAccountId }, intent);
  }

  cancel(invitation: Invitation, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(cancelInvitationPath, { id: invitation.userAccountId }, intent);
  }

  private async write(path: string, body: unknown, intent: IntentKey): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(this.api.put(path, body, intent));
    if (result.kind === 'ok') this.read.reload();
    return result;
  }
}
