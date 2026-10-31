import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AccountState,
  NewAccount,
  TwoStepFilter,
  UserAccount,
  UserFilters,
} from '@harmony/administration/domain';
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
const noFilters: UserFilters = {
  search: '',
  state: null,
  twoStep: null,
  roleId: null,
  page: FIRST_PAGE,
};
/** The whole platform fits one census page; Identity accepts 100 rows at most. */
const CENSUS_SIZE = 100;

/** The request Identity reads (GetPagedUserAccountsQuery). */
interface UsersRequest extends PageRequest {
  readonly searchText: string | null;
  readonly state: AccountState | null;
  readonly twoStep: TwoStepFilter | null;
  readonly roleId: string | null;
}

/** The accounts counted by state, for a lead (the console's People page). */
export interface AccountCensus {
  readonly total: number;
  readonly active: number;
  readonly suspended: number;
  readonly terminated: number;
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
      state: filters.state,
      twoStep: filters.twoStep,
      roleId: filters.roleId,
    };
    return { url: usersPath, method: 'POST', body };
  });
  /** The census is read only where a page asks for it. */
  private readonly censusWanted = signal(false);
  private readonly censusPage = httpResource<Envelope<Paged<UserAccount>>>(() => {
    if (!this.censusWanted()) return undefined;
    const body: UsersRequest = {
      pageNumber: FIRST_PAGE,
      pageSize: CENSUS_SIZE,
      searchText: null,
      state: null,
      twoStep: null,
      roleId: null,
    };
    return { url: usersPath, method: 'POST', body };
  });

  readonly rows = computed<readonly UserAccount[]>(() => this.page.value()?.data?.results ?? []);
  readonly total = computed(() => this.page.value()?.data?.totalCount ?? 0);
  /** Every platform account the census read (up to a page of a hundred); empty until asked for. */
  readonly everyone = computed<readonly UserAccount[]>(() =>
    this.censusPage.hasValue() ? (this.censusPage.value().data?.results ?? []) : [],
  );
  /** Every platform account counted by state; null until the census was asked for and read. */
  readonly census = computed<AccountCensus | null>(() => {
    if (!this.censusPage.hasValue()) return null;
    const data = this.censusPage.value().data;
    if (data === undefined) return null;
    const rows = data.results;
    return {
      total: data.totalCount,
      active: rows.filter((row) => row.state === AccountState.active).length,
      suspended: rows.filter((row) => row.state === AccountState.suspended).length,
      terminated: rows.filter((row) => row.state === AccountState.terminated).length,
    };
  });
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
    this.filters.update((filters) => ({ ...filters, search: text.trim(), page: FIRST_PAGE }));
  }

  showState(state: AccountState | null): void {
    this.filters.update((filters) => ({ ...filters, state, page: FIRST_PAGE }));
  }

  showTwoStep(twoStep: TwoStepFilter | null): void {
    this.filters.update((filters) => ({ ...filters, twoStep, page: FIRST_PAGE }));
  }

  showRole(roleId: string | null): void {
    this.filters.update((filters) => ({ ...filters, roleId, page: FIRST_PAGE }));
  }

  /** A page that leads with the counts asks once; the census is read again after every write. */
  countAll(): void {
    this.censusWanted.set(true);
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
    if (result.kind === 'ok') {
      this.page.reload();
      if (this.censusWanted()) this.censusPage.reload();
    }
    return result;
  }
}

/** True while the account may sign in. */
export const isActive = (user: UserAccount): boolean => user.state === AccountState.active;
