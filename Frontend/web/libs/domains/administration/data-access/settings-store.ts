import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { EffectiveSetting, SettingPlace } from '@harmony/administration/domain';
import { CurrentProperty } from '@harmony/platform/context';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';

/** The hotel the call names travels in X-Harmony-Property (the property interceptor); the path names nothing. */
export const effectivePath = '/api/ConfigValue/effective';
export const setValuePath = '/api/ConfigValue/Set';
export const clearValuePath = '/api/ConfigValue/Clear';

/**
 * One hotel's settings (design 5.1): every active key resolved at the hotel in the URL, each with its trace, and the
 * two writes. Provided by the page; the read follows the hotel in the URL, so a switch of hotel reads again, and a
 * write that succeeded reads again too.
 */
@Injectable()
export class SettingsStore {
  private readonly api = inject(ApiClient);
  private readonly current = inject(CurrentProperty);

  private readonly effective = httpResource<Envelope<EffectiveSetting[]>>(() =>
    // Read the hotel here so another hotel in the URL is another read; the interceptor names it on the call.
    this.current.code() === null ? undefined : { url: effectivePath },
  );

  readonly settings = computed<readonly EffectiveSetting[]>(
    () => this.effective.value()?.data ?? [],
  );
  readonly loading = this.effective.isLoading;
  readonly problem = computed(() => {
    const error = this.effective.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  reload(): void {
    this.effective.reload();
  }

  /** A value at a place (ConfigValue Set): 201 when the place had none, 200 when it changed. */
  set(
    key: string,
    place: SettingPlace,
    value: string,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      setValuePath,
      { key, scope: place.scope, scopeId: place.scopeId, value },
      intent,
    );
  }

  /** The value at a place goes; the next place down wins again. */
  clear(key: string, place: SettingPlace, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(clearValuePath, { key, scope: place.scope, scopeId: place.scopeId }, intent);
  }

  private async write(path: string, body: unknown, intent: IntentKey): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(this.api.put(path, body, intent));
    if (result.kind === 'ok') this.effective.reload();
    return result;
  }
}
