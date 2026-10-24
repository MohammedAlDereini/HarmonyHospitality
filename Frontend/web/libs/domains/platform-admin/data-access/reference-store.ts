import { HttpResourceRef, httpResource } from '@angular/common/http';
import { Injectable, Injector, Signal, computed, inject, signal } from '@angular/core';
import { ReferenceRow } from '@harmony/platform-admin/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { PageRequest, Paged, SortOrder } from '@harmony/shared/domain';
import { Observable, firstValueFrom } from 'rxjs';

/** A screen reads a table twenty rows at a time, as the platform app did; a select reads up to Identity's limit. */
export const REFERENCE_PAGE_SIZE = 20;
export const REFERENCE_OPTIONS_SIZE = 100;
const COUNT_ONLY = 1;
const FIRST_PAGE = 1;

export const listPath = (controller: string): string => `/api/${controller}/get-all`;
export const createPath = (controller: string): string => `/api/${controller}/Create`;
export const updatePath = (controller: string): string => `/api/${controller}/Update`;
export const rowPath = (controller: string, id: string): string =>
  `/api/${controller}/${encodeURIComponent(id)}`;

/** The filters a screen applies, by field: text as typed, "true"/"false" for a flag, a code for a lookup. */
export type ReferenceFilters = Readonly<Record<string, string>>;

/** What a screen may change about its reading of a table. */
export interface ReferenceQuery {
  readonly page?: number;
  readonly sort?: SortOrder | null;
  readonly filters?: ReferenceFilters;
  /** A table's own narrowing (lookup values by category); replaces the current scope. */
  readonly scope?: Readonly<Record<string, unknown>>;
}

/** The request every reference list reads (BasePaginationQueryRequest + SearchText + SortOrder, plus filters). */
interface ReferenceRequest extends PageRequest {
  readonly searchText: string | null;
  readonly sortOrder?: readonly SortOrder[];
}

/** One table's rows as a screen reads them: a page at a time, sorted, filtered, read again after every write. */
export interface TableRows {
  readonly rows: Signal<readonly ReferenceRow[]>;
  readonly total: Signal<number>;
  readonly loading: Signal<boolean>;
  readonly problem: Signal<string | null>;
  readonly page: Signal<number>;
  readonly sort: Signal<SortOrder | null>;
  readonly filters: Signal<ReferenceFilters>;
  readonly scope: Signal<Readonly<Record<string, unknown>>>;
  /** Reads the table (first call) or reads it again; a scope replaces the current one and goes back to page 1. */
  load(scope?: Readonly<Record<string, unknown>>): void;
  /** Changes the page, the sort or the filters; a new sort or filter goes back to page 1. */
  query(change: ReferenceQuery): void;
}

interface Table extends TableRows {
  readonly resource: HttpResourceRef<Envelope<Paged<ReferenceRow>> | undefined>;
}

/** The choices one select reads from another table, up to Identity's page limit, for one scope. */
export interface OptionRows {
  readonly rows: Signal<readonly ReferenceRow[]>;
  readonly loading: Signal<boolean>;
  load(scope?: Readonly<Record<string, unknown>>): void;
}

/** How many rows a table holds, for the overview tiles; null until known, 'denied' when the door refuses. */
export interface TableCount {
  readonly total: Signal<number | 'denied' | null>;
}

/** A filter value as Identity reads it: "true"/"false" become flags, anything else stays text. */
const asFilterValue = (value: string): unknown => {
  if (value === 'true') return true;
  if (value === 'false') return false;
  return value;
};

/**
 * The reference tables the console edits (architecture 7.8): one paged resource per table for the screen, one
 * wide resource per table for the selects, one count per table for the overview. Writes carry the press's intent key
 * and read the table (and its choices and count) again on success.
 */
@Injectable({ providedIn: 'root' })
export class ReferenceStore {
  private readonly api = inject(ApiClient);
  private readonly injector = inject(Injector);
  private readonly tables = new Map<string, Table>();
  private readonly options = new Map<string, OptionRows & { reload(): void }>();
  private readonly counts = new Map<string, TableCount & { reload(): void }>();

  /** The rows of one controller's table, as the screen reads them. */
  table(controller: string, defaultSort: readonly SortOrder[] = []): TableRows {
    const known = this.tables.get(controller);
    if (known !== undefined) return known;
    const made = this.makeTable(controller, defaultSort);
    this.tables.set(controller, made);
    return made;
  }

  /** The rows of one controller's table as choices for a select: the whole table, or one scope of it. */
  choices(controller: string): OptionRows {
    const known = this.options.get(controller);
    if (known !== undefined) return known;
    const made = this.makeChoices(controller);
    this.options.set(controller, made);
    return made;
  }

  /** How many rows a table holds. */
  count(controller: string): TableCount {
    const known = this.counts.get(controller);
    if (known !== undefined) return known;
    const made = this.makeCount(controller);
    this.counts.set(controller, made);
    return made;
  }

  create(
    controller: string,
    body: Readonly<Record<string, unknown>>,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(controller, this.api.post(createPath(controller), body, intent));
  }

  update(
    controller: string,
    body: Readonly<Record<string, unknown>>,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(controller, this.api.put(updatePath(controller), body, intent));
  }

  remove(controller: string, id: string, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(controller, this.api.delete(rowPath(controller, id), intent));
  }

  private makeTable(controller: string, defaultSort: readonly SortOrder[]): Table {
    const wanted = signal(false);
    const scope = signal<Readonly<Record<string, unknown>>>({});
    const page = signal(FIRST_PAGE);
    const sort = signal<SortOrder | null>(defaultSort[0] ?? null);
    const filters = signal<ReferenceFilters>({});
    const resource = httpResource<Envelope<Paged<ReferenceRow>>>(
      () => {
        if (!wanted()) return undefined;
        const chosen = sort();
        const applied = Object.fromEntries(
          Object.entries(filters())
            .filter(([key, value]) => key !== 'searchText' && value !== '')
            .map(([key, value]) => [key, asFilterValue(value)]),
        );
        const search = (filters()['searchText'] ?? '').trim();
        const body: ReferenceRequest & Record<string, unknown> = {
          pageNumber: page(),
          pageSize: REFERENCE_PAGE_SIZE,
          searchText: search === '' ? null : search,
          sortOrder: chosen === null ? defaultSort : [chosen],
          ...applied,
          ...scope(),
        };
        return { url: listPath(controller), method: 'POST', body };
      },
      { injector: this.injector },
    );
    const current = computed(() => (resource.hasValue() ? resource.value() : undefined));
    return {
      resource,
      scope,
      page,
      sort,
      filters,
      rows: computed(() => current()?.data?.results ?? []),
      total: computed(() => current()?.data?.totalCount ?? 0),
      loading: resource.isLoading,
      problem: computed(() => {
        const error = resource.error();
        return error === undefined ? null : messageOf(fromError(error));
      }),
      load: (next?: Readonly<Record<string, unknown>>) => {
        if (next !== undefined) {
          scope.set(next);
          page.set(FIRST_PAGE);
        }
        if (wanted()) resource.reload();
        else wanted.set(true);
      },
      query: (change: ReferenceQuery) => {
        if (change.scope !== undefined) scope.set(change.scope);
        if (change.sort !== undefined) sort.set(change.sort);
        if (change.filters !== undefined) filters.set(change.filters);
        if (change.page !== undefined) page.set(change.page);
        else if (change.sort !== undefined || change.filters !== undefined) page.set(FIRST_PAGE);
        if (!wanted()) wanted.set(true);
      },
    };
  }

  private makeChoices(controller: string): OptionRows & { reload(): void } {
    const wanted = signal(false);
    const scope = signal<Readonly<Record<string, unknown>>>({});
    const resource = httpResource<Envelope<Paged<ReferenceRow>>>(
      () => {
        if (!wanted()) return undefined;
        const body: ReferenceRequest & Record<string, unknown> = {
          pageNumber: FIRST_PAGE,
          pageSize: REFERENCE_OPTIONS_SIZE,
          searchText: null,
          ...scope(),
        };
        return { url: listPath(controller), method: 'POST', body };
      },
      { injector: this.injector },
    );
    const current = computed(() => (resource.hasValue() ? resource.value() : undefined));
    return {
      rows: computed(() => current()?.data?.results ?? []),
      loading: resource.isLoading,
      load: (next?: Readonly<Record<string, unknown>>) => {
        if (next !== undefined && JSON.stringify(next) !== JSON.stringify(scope())) scope.set(next);
        if (!wanted()) wanted.set(true);
      },
      reload: () => {
        if (wanted()) resource.reload();
      },
    };
  }

  private makeCount(controller: string): TableCount & { reload(): void } {
    const body: ReferenceRequest = {
      pageNumber: FIRST_PAGE,
      pageSize: COUNT_ONLY,
      searchText: null,
    };
    const resource = httpResource<Envelope<Paged<ReferenceRow>>>(
      () => ({ url: listPath(controller), method: 'POST', body }),
      { injector: this.injector },
    );
    return {
      total: computed(() => {
        if (resource.hasValue()) return resource.value().data?.totalCount ?? 0;
        const error = resource.error();
        if (error === undefined) return null;
        return fromError(error).kind === 'forbidden' ? 'denied' : null;
      }),
      reload: () => {
        resource.reload();
      },
    };
  }

  private async write(
    controller: string,
    call: Observable<ApiResult<unknown>>,
  ): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(call);
    if (result.kind === 'ok') {
      this.tables.get(controller)?.resource.reload();
      this.options.get(controller)?.reload();
      this.counts.get(controller)?.reload();
    }
    return result;
  }
}
