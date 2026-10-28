import { Injectable, computed, inject, signal } from '@angular/core';
import { ApiClient, ApiResult, IntentKey, newIntentKey } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';

export const confirmIdentityPath = '/api/UserAccount/ConfirmIdentity';

/** Identity's answer when a change needs the person to confirm it is them first (IdentityConfirmationRequired). */
export const CONFIRMATION_REQUIRED = '00084';

const needsConfirmation = (result: ApiResult<unknown>): boolean =>
  result.kind === 'refused' && result.codes.includes(CONFIRMATION_REQUIRED);

/**
 * "Confirm it is you" (stage B) for the changes Identity guards while a customer's approvals are on: the first answer
 * is 00084, the shell's dialog asks for the password, Identity records a 5-minute proof, and the change is sent once
 * more with a new intent key (the first one was answered and kept). One question at a time.
 */
@Injectable({ providedIn: 'root' })
export class IdentityConfirmation {
  private readonly api = inject(ApiClient);
  private readonly waiting = signal<((confirmed: boolean) => void) | null>(null);

  /** True while the shell's dialog should be open. */
  readonly asking = computed(() => this.waiting() !== null);

  /** Sends a guarded change; when Identity first wants the person to confirm, asks once and sends it again. */
  async run<T>(call: (intent: IntentKey) => Promise<ApiResult<T>>): Promise<ApiResult<T>> {
    const first = await call(newIntentKey());
    if (!needsConfirmation(first)) return first;
    return (await this.ask()) ? call(newIntentKey()) : first;
  }

  /** Opens the dialog; the answer is true once the password was accepted, false when the person stays. */
  ask(): Promise<boolean> {
    if (this.waiting() !== null) return Promise.resolve(false);
    return new Promise((resolve) => {
      this.waiting.set(resolve);
    });
  }

  /** The password, to Identity; on success the waiting change goes ahead. */
  async confirm(password: string, intent: IntentKey): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(this.api.put(confirmIdentityPath, { password }, intent));
    if (result.kind === 'ok') this.settle(true);
    return result;
  }

  dismiss(): void {
    this.settle(false);
  }

  private settle(confirmed: boolean): void {
    const resolve = this.waiting();
    this.waiting.set(null);
    resolve?.(confirmed);
  }
}
