import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Device, Passkey, SecurityEvent, UserAccount } from '@harmony/administration/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { Observable, firstValueFrom } from 'rxjs';

export const accountPath = (id: string): string => `/api/UserAccount/${id}`;
export const devicesPath = (id: string): string => `${accountPath(id)}/devices`;
export const passkeysPath = (id: string): string => `${accountPath(id)}/passkeys`;
export const eventsPath = (id: string): string => `${accountPath(id)}/security-events`;
export const updateRolesPath = '/api/UserAccount/UpdateRoles';
export const resetTwoFactorPath = '/api/UserAccount/ResetTwoFactor';
export const endExemptionPath = '/api/UserAccount/EndTwoStepExemption';
export const signOutDevicePath = '/api/UserAccount/SignOutDevice';
export const removePasskeyPath = '/api/UserAccount/RemovePasskey';

/**
 * One person's page (architecture 7.8): the account, its devices, passkeys and security trail, each its own
 * resource keyed by the account id, each read again after the write that changes it. Provided by the page, so it
 * lives and dies with it.
 */
@Injectable()
export class PersonStore {
  private readonly api = inject(ApiClient);
  private readonly id = signal<string | null>(null);

  private readonly account = httpResource<Envelope<UserAccount>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: accountPath(id) };
  });
  private readonly devices = httpResource<Envelope<Device[]>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: devicesPath(id) };
  });
  private readonly passkeys = httpResource<Envelope<Passkey[]>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: passkeysPath(id) };
  });
  private readonly events = httpResource<Envelope<SecurityEvent[]>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: eventsPath(id) };
  });

  readonly person = computed<UserAccount | null>(() => this.account.value()?.data ?? null);
  readonly loading = this.account.isLoading;
  readonly problem = computed(() => this.sentence(this.account.error()));
  readonly deviceRows = computed<readonly Device[]>(() => this.devices.value()?.data ?? []);
  readonly devicesProblem = computed(() => this.sentence(this.devices.error()));
  readonly passkeyRows = computed<readonly Passkey[]>(() => this.passkeys.value()?.data ?? []);
  readonly passkeysProblem = computed(() => this.sentence(this.passkeys.error()));
  readonly eventRows = computed<readonly SecurityEvent[]>(() => this.events.value()?.data ?? []);
  readonly eventsProblem = computed(() => this.sentence(this.events.error()));

  /** The page names the account; a different id starts the four reads afresh. */
  open(id: string): void {
    this.id.set(id);
  }

  reload(): void {
    this.account.reload();
  }

  /** The account holds exactly these roles afterwards (UpdateUserRolesCommand). */
  updateRoles(
    user: UserAccount,
    roleIds: readonly string[],
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(updateRolesPath, { id: user.id, roleIds }, intent), () => {
      this.account.reload();
      this.events.reload();
    });
  }

  /** Clears the authenticator; the person enrols again at the next sign-in. */
  resetTwoFactor(user: UserAccount, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(resetTwoFactorPath, { id: user.id }, intent), () => {
      this.account.reload();
      this.events.reload();
    });
  }

  endExemption(user: UserAccount, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(endExemptionPath, { id: user.id }, intent), () => {
      this.account.reload();
      this.events.reload();
    });
  }

  signOutDevice(user: UserAccount, device: Device, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.put(signOutDevicePath, { id: user.id, deviceId: device.id }, intent),
      () => {
        this.devices.reload();
        this.events.reload();
      },
    );
  }

  removePasskey(
    user: UserAccount,
    passkey: Passkey,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.put(removePasskeyPath, { id: user.id, passkeyId: passkey.id }, intent),
      () => {
        this.passkeys.reload();
        this.events.reload();
      },
    );
  }

  private sentence(error: unknown): string | null {
    return error === undefined ? null : messageOf(fromError(error));
  }

  /** A write that succeeded changed something: read it again. The outcome goes back to the screen as it is. */
  private async write(
    call: Observable<ApiResult<unknown>>,
    after: () => void,
  ): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(call);
    if (result.kind === 'ok') after();
    return result;
  }
}
