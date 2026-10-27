import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { CompanyLogin } from '@harmony/administration/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';

export const myCompanyLoginsPath = '/api/CompanyLogin/mine';
export const addDomainPath = '/api/CompanyLogin/AddDomain';
export const removeDomainPath = '/api/CompanyLogin/RemoveDomain';
export const switchCompanyLoginPath = '/api/CompanyLogin/Switch';

/**
 * The customer's company login (stage A, the Mews way): Harmony adds Microsoft or Google; the administrator lists the
 * company's e-mail domains and switches it on. Provided by the page; every write that succeeded reads again.
 */
@Injectable()
export class CompanyLoginStore {
  private readonly api = inject(ApiClient);
  private readonly logins = httpResource<Envelope<CompanyLogin[]>>(() => ({
    url: myCompanyLoginsPath,
  }));

  readonly rows = computed<readonly CompanyLogin[]>(() => this.logins.value()?.data ?? []);
  readonly loading = this.logins.isLoading;
  readonly problem = computed(() => {
    const error = this.logins.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  reload(): void {
    this.logins.reload();
  }

  addDomain(login: CompanyLogin, domain: string, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(addDomainPath, { id: login.id, domain }, intent);
  }

  removeDomain(
    login: CompanyLogin,
    domain: string,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(removeDomainPath, { id: login.id, domain }, intent);
  }

  /** On: the listed domains sign in with the company from their next sign-in. Off: everybody uses a password again. */
  switch(login: CompanyLogin, on: boolean, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(switchCompanyLoginPath, { id: login.id, on }, intent);
  }

  private async write(path: string, body: unknown, intent: IntentKey): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(this.api.put(path, body, intent));
    if (result.kind === 'ok') this.logins.reload();
    return result;
  }
}
