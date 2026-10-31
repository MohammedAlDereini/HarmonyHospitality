import { httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';
import { TenantChoice } from '@harmony/platform-operations/domain';
import { messageOf } from '@harmony/platform/errors';
import { Envelope, fromError } from '@harmony/platform/http';
import { ASCENDING, PageRequest, Paged, SortOrder } from '@harmony/shared/domain';

export const tenantChoicesPath = '/api/Tenant/get-all';
/** The most Identity hands out in one page; a platform with more customers than this filters by search instead. */
export const TENANT_CHOICES_PAGE_SIZE = 100;
const FIRST_PAGE = 1;

interface ChoicesRequest extends PageRequest {
  readonly sortOrder: readonly SortOrder[];
  readonly searchText: null;
  readonly status: null;
}

const byCode: readonly SortOrder[] = [{ memberName: 'Code', sortOrder: ASCENDING }];

/**
 * The customers as a filter lists them, by code: one read of the first hundred, shared by the console's filters
 * (architecture 7.8). When there are more, the list says so and the search box takes over.
 */
@Injectable({ providedIn: 'root' })
export class TenantChoicesStore {
  private readonly page = httpResource<Envelope<Paged<TenantChoice>>>(() => {
    const body: ChoicesRequest = {
      pageNumber: FIRST_PAGE,
      pageSize: TENANT_CHOICES_PAGE_SIZE,
      sortOrder: byCode,
      searchText: null,
      status: null,
    };
    return { url: tenantChoicesPath, method: 'POST', body };
  });

  readonly rows = computed<readonly TenantChoice[]>(() =>
    this.page.hasValue() ? (this.page.value().data?.results ?? []) : [],
  );
  /** True when the platform has more customers than one page names. */
  readonly truncated = computed(
    () =>
      this.page.hasValue() && (this.page.value().data?.totalCount ?? 0) > TENANT_CHOICES_PAGE_SIZE,
  );
  readonly loading = this.page.isLoading;
  readonly problem = computed(() => {
    const error = this.page.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  reload(): void {
    this.page.reload();
  }
}
