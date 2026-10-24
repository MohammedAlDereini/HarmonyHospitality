import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { AccountState, UserAccount, UserFilters } from '@harmony/administration/domain';
import { PropertyScope } from '@harmony/platform/context';
import { messageOf } from '@harmony/platform/errors';
import { Envelope, fromError } from '@harmony/platform/http';
import { PageRequest, Paged } from '@harmony/shared/domain';

export const usersPath = '/api/UserAccount/get-all';
/** Identity accepts 1 to 100 rows a page. */
export const USERS_PAGE_SIZE = 25;
const FIRST_PAGE = 1;
const noFilters: UserFilters = { search: '', page: FIRST_PAGE };

/** The request Identity reads (GetPagedUserAccountsQuery). */
interface UsersRequest extends PageRequest {
  readonly searchText: string | null;
}

/**
 * The accounts list (architecture 7.8): one resource keyed by the filters; a key change cancels the old request.
 * Accounts are tenant-wide, so a property switch only resets the filters.
 */
@Injectable({ providedIn: 'root' })
export class UsersStore {
  readonly filters = signal<UserFilters>(noFilters);

  private readonly page = httpResource<Envelope<Paged<UserAccount>>>(() => {
    const filters = this.filters();
    const body: UsersRequest = {
      pageNumber: filters.page,
      pageSize: USERS_PAGE_SIZE,
      searchText: filters.search === '' ? null : filters.search,
    };
    return { url: usersPath, method: 'POST', body };
  });

  readonly rows = computed<readonly UserAccount[]>(() => this.page.value()?.data?.results ?? []);
  readonly total = computed(() => this.page.value()?.data?.totalCount ?? 0);
  readonly loading = this.page.isLoading;
  /** One sentence when the read failed, in the person's language; null while all is well. */
  readonly problem = computed(() => {
    const error = this.page.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  constructor() {
    inject(PropertyScope).onReset(() => {
      this.filters.set(noFilters);
    });
  }

  search(text: string): void {
    this.filters.set({ search: text.trim(), page: FIRST_PAGE });
  }

  goTo(page: number): void {
    this.filters.update((filters) => ({ ...filters, page }));
  }

  reload(): void {
    this.page.reload();
  }
}

/** True while the account may sign in. */
export const isActive = (user: UserAccount): boolean => user.state === AccountState.active;
