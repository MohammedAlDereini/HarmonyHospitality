import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  SubscriptionDraft,
  Tenant,
  TenantDraft,
  TenantFilters,
  TenantStatus,
} from '@harmony/platform-admin/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { PageRequest, Paged } from '@harmony/shared/domain';
import { Observable, firstValueFrom } from 'rxjs';

export const tenantsPath = '/api/Tenant/get-all';
export const createTenantPath = '/api/Tenant/Create';
export const updateTenantPath = '/api/Tenant/Update';
export const suspendTenantPath = '/api/Tenant/Suspend';
export const reinstateTenantPath = '/api/Tenant/Reinstate';
export const inviteAdministratorPath = '/api/Tenant/InviteAdministrator';
export const subscribePath = '/api/Tenant/Subscribe';
export const unsubscribePath = '/api/Tenant/Unsubscribe';
export const tenantPath = (id: string): string => `/api/Tenant/${id}`;
/** Identity accepts 1 to 100 rows a page. */
export const TENANTS_PAGE_SIZE = 25;
const FIRST_PAGE = 1;
const noFilters: TenantFilters = { search: '', status: null, page: FIRST_PAGE };

/** The request Identity reads (GetPagedTenantsQuery). */
interface TenantsRequest extends PageRequest {
  readonly searchText: string | null;
  readonly status: TenantStatus | null;
}

/**
 * The platform's customers (architecture 7.8): one resource keyed by the filters; every write carries the press's
 * intent key and reads the list again on success.
 */
@Injectable({ providedIn: 'root' })
export class TenantsStore {
  private readonly api = inject(ApiClient);
  readonly filters = signal<TenantFilters>(noFilters);

  private readonly page = httpResource<Envelope<Paged<Tenant>>>(() => {
    const filters = this.filters();
    const body: TenantsRequest = {
      pageNumber: filters.page,
      pageSize: TENANTS_PAGE_SIZE,
      searchText: filters.search === '' ? null : filters.search,
      status: filters.status,
    };
    return { url: tenantsPath, method: 'POST', body };
  });

  readonly rows = computed<readonly Tenant[]>(() => this.page.value()?.data?.results ?? []);
  readonly total = computed(() => this.page.value()?.data?.totalCount ?? 0);
  readonly loading = this.page.isLoading;
  /** One sentence when the read failed, in the person's language; null while all is well. */
  readonly problem = computed(() => {
    const error = this.page.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  search(text: string): void {
    this.filters.update((filters) => ({ ...filters, search: text.trim(), page: FIRST_PAGE }));
  }

  showStatus(status: TenantStatus | null): void {
    this.filters.update((filters) => ({ ...filters, status, page: FIRST_PAGE }));
  }

  goTo(page: number): void {
    this.filters.update((filters) => ({ ...filters, page }));
  }

  reload(): void {
    this.page.reload();
  }

  /** One tenant with its subscription lines. */
  detail(id: string): Promise<ApiResult<Tenant>> {
    return firstValueFrom(this.api.get<Tenant>(tenantPath(id)));
  }

  /** A new customer; Identity creates the first administrator and mails the invitation. */
  create(draft: TenantDraft, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.post(createTenantPath, this.body(draft), intent));
  }

  /** The code and the first administrator never change here (EditTenantCommand). */
  update(tenant: Tenant, draft: TenantDraft, intent: IntentKey): Promise<ApiResult<unknown>> {
    const body = {
      id: tenant.id,
      legalNameEn: draft.legalNameEn,
      legalNameAr: draft.legalNameAr,
      countryCode: draft.countryCode,
      defaultCurrency: draft.defaultCurrency,
      tier: draft.tier,
      externalIdentifier: draft.externalIdentifier === '' ? null : draft.externalIdentifier,
    };
    return this.write(this.api.put(updateTenantPath, body, intent));
  }

  suspend(tenant: Tenant, reason: string, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(suspendTenantPath, { id: tenant.id, reason }, intent));
  }

  reinstate(tenant: Tenant, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(reinstateTenantPath, { id: tenant.id }, intent));
  }

  /** Mails the administrator's invitation again, to the same or to a new person. */
  invite(
    tenant: Tenant,
    administratorEmail: string,
    administratorDisplayName: string,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    const body = {
      id: tenant.id,
      administratorEmail: administratorEmail === '' ? null : administratorEmail,
      administratorDisplayName: administratorDisplayName === '' ? null : administratorDisplayName,
    };
    return this.write(this.api.post(inviteAdministratorPath, body, intent));
  }

  subscribe(
    tenant: Tenant,
    draft: SubscriptionDraft,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(this.api.post(subscribePath, { tenantId: tenant.id, ...draft }, intent));
  }

  unsubscribe(
    tenant: Tenant,
    line: Pick<SubscriptionDraft, 'propertyId' | 'moduleCode'>,
    reason: string,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    const body = {
      tenantId: tenant.id,
      propertyId: line.propertyId,
      moduleCode: line.moduleCode,
      reason,
    };
    return this.write(this.api.put(unsubscribePath, body, intent));
  }

  /** Identity wants null, not an empty string, for what was not given. */
  private body(draft: TenantDraft): Record<string, unknown> {
    return {
      code: draft.code,
      legalNameEn: draft.legalNameEn,
      legalNameAr: draft.legalNameAr,
      countryCode: draft.countryCode,
      defaultCurrency: draft.defaultCurrency,
      tier: draft.tier,
      externalIdentifier: draft.externalIdentifier === '' ? null : draft.externalIdentifier,
      administratorEmail: draft.administratorEmail,
      administratorDisplayName: draft.administratorDisplayName,
    };
  }

  /** A write that succeeded changes the list; read it again. The outcome goes back to the screen as it is. */
  private async write(call: Observable<ApiResult<unknown>>): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(call);
    if (result.kind === 'ok') this.page.reload();
    return result;
  }
}
