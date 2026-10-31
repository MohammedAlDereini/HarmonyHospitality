import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { MyDevice } from '@harmony/platform-operations/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';

export const myDevicesPath = '/api/UserAccount/MyDevices';
export const signOutDevicePath = '/api/UserAccount/SignOutMyDevice';

/**
 * The browsers the signed-in person used, most recent first, and the door that signs one out (the Sign-ins page's
 * "Your sessions" rail). Provided by the page; a sign-out that succeeded reads the list again.
 */
@Injectable()
export class MyDevicesStore {
  private readonly api = inject(ApiClient);
  private readonly read = httpResource<Envelope<MyDevice[]>>(() => ({ url: myDevicesPath }));

  readonly rows = computed<readonly MyDevice[]>(() =>
    [...(this.read.hasValue() ? (this.read.value().data ?? []) : [])].sort((a, b) =>
      b.lastSeenUtc.localeCompare(a.lastSeenUtc),
    ),
  );
  readonly signedIn = computed(() => this.rows().filter((device) => device.signedIn).length);
  readonly loading = this.read.isLoading;
  readonly problem = computed(() => {
    const error = this.read.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  reload(): void {
    this.read.reload();
  }

  /** Ends the session still open on that browser through the caller's own door (SignOutMyDevice); the device stays known. */
  async signOut(device: MyDevice, intent: IntentKey): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(
      this.api.put(signOutDevicePath, { deviceId: device.id }, intent),
    );
    if (result.kind === 'ok') this.read.reload();
    return result;
  }
}
