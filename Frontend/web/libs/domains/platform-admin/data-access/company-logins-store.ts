import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CompanyProvider, TenantCompanyLogin } from '@harmony/platform-admin/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';

export const tenantCompanyLoginsPath = (tenantId: string): string =>
  `/api/CompanyLogin/of/${tenantId}`;
export const addCompanyLoginPath = '/api/CompanyLogin/Add';

/**
 * One customer's company logins, for the console (stage A): the platform adds Microsoft or Google with one of the
 * company's domains; the customer's administrator does the rest in the web app. Provided by the panel.
 */
@Injectable()
export class CompanyLoginsStore {
  private readonly api = inject(ApiClient);
  private readonly tenantId = signal<string | null>(null);
  private readonly logins = httpResource<Envelope<TenantCompanyLogin[]>>(() => {
    const id = this.tenantId();
    return id === null ? undefined : { url: tenantCompanyLoginsPath(id) };
  });

  readonly rows = computed<readonly TenantCompanyLogin[]>(() => this.logins.value()?.data ?? []);
  readonly problem = computed(() => {
    const error = this.logins.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  ofTenant(id: string): void {
    this.tenantId.set(id);
  }

  /** Microsoft or Google for the customer, with one of the company's e-mail domains; it starts switched off. */
  async add(
    provider: CompanyProvider,
    domain: string,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    const tenantId = this.tenantId();
    const result = await firstValueFrom(
      this.api.post(addCompanyLoginPath, { tenantId, provider, domain }, intent),
    );
    if (result.kind === 'ok') this.logins.reload();
    return result;
  }
}
