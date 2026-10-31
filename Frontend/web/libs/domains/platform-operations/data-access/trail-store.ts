import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { TrailCount, TrailEvent, TrailFilters } from '@harmony/platform-operations/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, fromError } from '@harmony/platform/http';
import { DESCENDING, PageRequest, Paged, SortOrder } from '@harmony/shared/domain';
import { firstValueFrom } from 'rxjs';

export const trailPath = '/api/SecurityAudit/get-all';
export const trailCountsPath = '/api/SecurityAudit/counts';
/** Identity accepts 1 to 100 rows a page. */
export const TRAIL_PAGE_SIZE = 25;
const FIRST_PAGE = 1;
const noFilters: TrailFilters = {
  search: '',
  action: null,
  actions: [],
  tenantId: null,
  userAccountId: null,
  actorSubject: null,
  from: null,
  to: null,
  page: FIRST_PAGE,
};
/** Newest first, as the Audit log reads it. */
const newestFirst: readonly SortOrder[] = [{ memberName: 'OccurredAtUtc', sortOrder: DESCENDING }];

/** The request Identity reads (GetPagedPlatformTrailQuery). */
interface TrailRequest extends PageRequest {
  readonly sortOrder: readonly SortOrder[];
  readonly searchText: string | null;
  readonly action: string | null;
  readonly actions: readonly string[] | null;
  readonly tenantId: string | null;
  readonly userAccountId: string | null;
  readonly actorSubject: string | null;
  readonly fromUtc: string | null;
  readonly toUtc: string | null;
}

/**
 * The platform's trail across every customer (architecture 7.8): one resource keyed by the filters, newest first. A page
 * provides its own store and may pin a family (the Sign-ins page pins "sign-in."): the person's event filter then narrows
 * inside it. Reads only: the trail is never written from here.
 */
@Injectable()
export class TrailStore {
  private readonly api = inject(ApiClient);
  readonly filters = signal<TrailFilters>(noFilters);
  /** The family every read keeps to, or null for the whole trail. */
  readonly family = signal<string | null>(null);
  readonly pageSize = signal(TRAIL_PAGE_SIZE);

  private readonly page = httpResource<Envelope<Paged<TrailEvent>>>(() => {
    const filters = this.filters();
    const body: TrailRequest = {
      pageNumber: filters.page,
      pageSize: this.pageSize(),
      sortOrder: newestFirst,
      searchText: filters.search === '' ? null : filters.search,
      action: filters.action ?? this.family(),
      actions: filters.actions.length === 0 ? null : filters.actions,
      tenantId: filters.tenantId,
      userAccountId: filters.userAccountId,
      actorSubject: filters.actorSubject,
      fromUtc: filters.from,
      toUtc: filters.to,
    };
    return { url: trailPath, method: 'POST', body };
  });

  readonly rows = computed<readonly TrailEvent[]>(() =>
    this.page.hasValue() ? (this.page.value().data?.results ?? []) : [],
  );
  readonly total = computed(() =>
    this.page.hasValue() ? (this.page.value().data?.totalCount ?? 0) : 0,
  );
  readonly loading = this.page.isLoading;
  /** One sentence when the read failed, in the person's language; null while all is well. */
  readonly problem = computed(() => {
    const error = this.page.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  search(text: string): void {
    this.filters.update((filters) => ({ ...filters, search: text.trim(), page: FIRST_PAGE }));
  }

  /** One action or a family inside the pinned one; null for everything the store keeps to. */
  showAction(action: string | null): void {
    this.filters.update((filters) => ({ ...filters, action, page: FIRST_PAGE }));
  }

  /** Only these actions (an outcome's set); empty for no narrowing. */
  showActions(actions: readonly string[]): void {
    this.filters.update((filters) => ({ ...filters, actions, page: FIRST_PAGE }));
  }

  showTenant(tenantId: string | null): void {
    this.filters.update((filters) => ({ ...filters, tenantId, page: FIRST_PAGE }));
  }

  showPerson(userAccountId: string | null): void {
    this.filters.update((filters) => ({ ...filters, userAccountId, page: FIRST_PAGE }));
  }

  /** Only what one actor did (a platform person's account id, or "system"); null for everyone. */
  showActor(actorSubject: string | null): void {
    this.filters.update((filters) => ({ ...filters, actorSubject, page: FIRST_PAGE }));
  }

  /** The window, as ISO instants in UTC; null on either side leaves that side open. */
  between(from: string | null, to: string | null): void {
    this.filters.update((filters) => ({ ...filters, from, to, page: FIRST_PAGE }));
  }

  goTo(page: number): void {
    this.filters.update((filters) => ({ ...filters, page }));
  }

  reload(): void {
    this.page.reload();
  }

  /** How often each action was written in a window (Identity: the last 24 hours when both sides are null). */
  counts(
    from: string | null,
    to: string | null,
    action: string | null,
  ): Promise<ApiResult<TrailCount[]>> {
    const query = new URLSearchParams();
    if (from !== null) query.set('fromUtc', from);
    if (to !== null) query.set('toUtc', to);
    if (action !== null) query.set('action', action);
    const text = query.toString();
    return firstValueFrom(
      this.api.get<TrailCount[]>(text === '' ? trailCountsPath : `${trailCountsPath}?${text}`),
    );
  }
}
