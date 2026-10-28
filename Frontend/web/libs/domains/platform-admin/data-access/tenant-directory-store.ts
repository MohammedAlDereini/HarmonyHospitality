import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { TenantDirectory } from '@harmony/platform-admin/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';

export const tenantDirectoryPath = (tenantId: string): string => `/api/Directory/of/${tenantId}`;
export const removeDirectoryPath = '/api/Directory/Remove';

/**
 * One customer's company directory, for the console (stage E): what is connected, how many people and groups, and the
 * platform's removal (Mews: Customer Success removes SCIM on purpose; the people go back to the customer's
 * administrators). Provided by the panel.
 */
@Injectable()
export class TenantDirectoryStore {
  private readonly api = inject(ApiClient);
  private readonly tenantId = signal<string | null>(null);
  private readonly read = httpResource<Envelope<TenantDirectory | null>>(() => {
    const id = this.tenantId();
    return id === null ? undefined : { url: tenantDirectoryPath(id) };
  });

  readonly directory = computed<TenantDirectory | null>(() => this.read.value()?.data ?? null);
  readonly problem = computed(() => {
    const error = this.read.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  ofTenant(id: string): void {
    this.tenantId.set(id);
  }

  async remove(intent: IntentKey): Promise<ApiResult<unknown>> {
    const id = this.tenantId();
    if (id === null) throw new Error('No customer chosen.');
    const result = await firstValueFrom(
      this.api.put(removeDirectoryPath, { tenantId: id }, intent),
    );
    if (result.kind === 'ok') this.read.reload();
    return result;
  }
}
