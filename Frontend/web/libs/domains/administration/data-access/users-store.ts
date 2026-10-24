import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { AccountState, NewAccount, UserAccount, UserFilters } from '@harmony/administration/domain';
import { PropertyScope } from '@harmony/platform/context';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { Observable, firstValueFrom } from 'rxjs';
import { PageRequest, Paged } from '@harmony/shared/domain';

export const usersPath = '/api/UserAccount/get-all';
export const suspendPath = '/api/UserAccount/Suspend';
export const reinstatePath = '/api/UserAccount/Reinstate';
export const createPath = '/api/UserAccount/Create';
export const terminatePath = '/api/UserAccount/Terminate';
export const exemptPath = '/api/UserAccount/ExemptFromTwoStep';
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
  private readonly api = inject(ApiClient);
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

  /** Suspends an account with a reason; the intent key is the press's own, reused only for its retries (11.5). */
  suspend(user: UserAccount, reason: string, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(suspendPath, { id: user.id, reason }, intent));
  }

  reinstate(user: UserAccount, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(reinstatePath, { id: user.id }, intent));
  }

  /** Final: Identity destroys the credential and kills the tokens; a terminated account is never reinstated. */
  terminate(user: UserAccount, reason: string, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(terminatePath, { id: user.id, reason }, intent));
  }

  /** Puts the person on the two-step exception list, with the reason the list keeps. */
  exempt(user: UserAccount, reason: string, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(exemptPath, { id: user.id, reason }, intent));
  }

  /** A new account with one role and no password: Identity mails the invitation (CreateUserAccountCommand). */
  create(account: NewAccount, intent: IntentKey): Promise<ApiResult<unknown>> {
    const body = {
      displayName: account.displayName,
      email: account.email,
      roleIds: [account.roleId],
    };
    return this.write(this.api.post(createPath, body, intent));
  }

  /** A write that succeeded changes the list; read it again. The outcome goes back to the screen as it is. */
  private async write(call: Observable<ApiResult<unknown>>): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(call);
    if (result.kind === 'ok') this.page.reload();
    return result;
  }
}

/** True while the account may sign in. */
export const isActive = (user: UserAccount): boolean => user.state === AccountState.active;
