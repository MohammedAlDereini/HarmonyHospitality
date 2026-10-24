import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  Property,
  PropertyDraft,
  PropertyFilters,
  PropertyUpdate,
} from '@harmony/platform-admin/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { PageRequest, Paged } from '@harmony/shared/domain';
import { Observable, firstValueFrom } from 'rxjs';

export const propertiesPath = '/api/Property/get-all';
export const createPropertyPath = '/api/Property/Create';
export const updatePropertyPath = '/api/Property/Update';
export const propertyPath = (id: string): string => `/api/Property/${id}`;
/** Identity accepts 1 to 100 rows a page. */
export const PROPERTIES_PAGE_SIZE = 25;
const FIRST_PAGE = 1;
const noFilters: PropertyFilters = { tenantId: null, search: '', page: FIRST_PAGE };

/** The request Identity reads (GetPagedPropertiesQuery). */
interface PropertiesRequest extends PageRequest {
  readonly tenantId: string | null;
  readonly searchText: string | null;
  readonly isActive: boolean | null;
}

/** Every customer's properties (architecture 7.8): one resource keyed by the filters; writes read the list again. */
@Injectable({ providedIn: 'root' })
export class PropertiesStore {
  private readonly api = inject(ApiClient);
  readonly filters = signal<PropertyFilters>(noFilters);

  private readonly page = httpResource<Envelope<Paged<Property>>>(() => {
    const filters = this.filters();
    const body: PropertiesRequest = {
      pageNumber: filters.page,
      pageSize: PROPERTIES_PAGE_SIZE,
      tenantId: filters.tenantId,
      searchText: filters.search === '' ? null : filters.search,
      isActive: null,
    };
    return { url: propertiesPath, method: 'POST', body };
  });

  readonly rows = computed<readonly Property[]>(() => this.page.value()?.data?.results ?? []);
  readonly total = computed(() => this.page.value()?.data?.totalCount ?? 0);
  readonly loading = this.page.isLoading;
  readonly problem = computed(() => {
    const error = this.page.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  search(text: string): void {
    this.filters.update((filters) => ({ ...filters, search: text.trim(), page: FIRST_PAGE }));
  }

  ofTenant(tenantId: string | null): void {
    this.filters.update((filters) => ({ ...filters, tenantId, page: FIRST_PAGE }));
  }

  goTo(page: number): void {
    this.filters.update((filters) => ({ ...filters, page }));
  }

  reload(): void {
    this.page.reload();
  }

  detail(id: string): Promise<ApiResult<Property>> {
    return firstValueFrom(this.api.get<Property>(propertyPath(id)));
  }

  create(draft: PropertyDraft, intent: IntentKey): Promise<ApiResult<unknown>> {
    const body = {
      ...draft,
      postalCode: draft.postalCode === '' ? null : draft.postalCode,
      externalIdentifier: draft.externalIdentifier === '' ? null : draft.externalIdentifier,
    };
    return this.write(this.api.post(createPropertyPath, body, intent));
  }

  update(
    property: Property,
    update: PropertyUpdate,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    const body = {
      id: property.id,
      ...update,
      postalCode: update.postalCode === '' ? null : update.postalCode,
    };
    return this.write(this.api.put(updatePropertyPath, body, intent));
  }

  private async write(call: Observable<ApiResult<unknown>>): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(call);
    if (result.kind === 'ok') this.page.reload();
    return result;
  }
}
