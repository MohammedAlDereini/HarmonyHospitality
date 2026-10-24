import { HttpResourceRef, httpResource } from '@angular/common/http';
import { Injectable, Injector, Signal, computed, inject, signal } from '@angular/core';
import { ReferenceRow } from '@harmony/platform-admin/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { PageRequest, Paged } from '@harmony/shared/domain';
import { Observable, firstValueFrom } from 'rxjs';

/** Identity accepts 1 to 100 rows a page; a reference table is read whole, page by page is for later. */
export const REFERENCE_PAGE_SIZE = 100;
const FIRST_PAGE = 1;

export const listPath = (controller: string): string => `/api/${controller}/get-all`;
export const createPath = (controller: string): string => `/api/${controller}/Create`;
export const updatePath = (controller: string): string => `/api/${controller}/Update`;
export const rowPath = (controller: string, id: string): string =>
  `/api/${controller}/${encodeURIComponent(id)}`;

/** The request every reference list reads (BasePaginationQueryRequest + SearchText, plus a table's own scope). */
interface ReferenceRequest extends PageRequest {
  readonly searchText: string | null;
}

/** One table's rows, read when first asked and again after every write; a scope narrows it (lookup values). */
export interface TableRows {
  readonly rows: Signal<readonly ReferenceRow[]>;
  readonly loading: Signal<boolean>;
  readonly problem: Signal<string | null>;
  readonly scope: Signal<Readonly<Record<string, unknown>>>;
  load(scope?: Readonly<Record<string, unknown>>): void;
}

interface Table extends TableRows {
  readonly resource: HttpResourceRef<Envelope<Paged<ReferenceRow>> | undefined>;
}

/**
 * The reference tables the console edits (architecture 7.8): one resource per table, created when a screen first
 * asks for it. Writes carry the press's intent key and read that table again on success.
 */
@Injectable({ providedIn: 'root' })
export class ReferenceStore {
  private readonly api = inject(ApiClient);
  private readonly injector = inject(Injector);
  private readonly tables = new Map<string, Table>();

  /** The rows of one controller's table. */
  table(controller: string): TableRows {
    const known = this.tables.get(controller);
    if (known !== undefined) return known;
    const made = this.makeTable(controller);
    this.tables.set(controller, made);
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

  private makeTable(controller: string): Table {
    const wanted = signal(false);
    const scope = signal<Readonly<Record<string, unknown>>>({});
    const resource = httpResource<Envelope<Paged<ReferenceRow>>>(
      () => {
        if (!wanted()) return undefined;
        const body: ReferenceRequest & Record<string, unknown> = {
          pageNumber: FIRST_PAGE,
          pageSize: REFERENCE_PAGE_SIZE,
          searchText: null,
          ...scope(),
        };
        return { url: listPath(controller), method: 'POST', body };
      },
      { injector: this.injector },
    );
    return {
      resource,
      scope,
      rows: computed(() => resource.value()?.data?.results ?? []),
      loading: resource.isLoading,
      problem: computed(() => {
        const error = resource.error();
        return error === undefined ? null : messageOf(fromError(error));
      }),
      load: (next?: Readonly<Record<string, unknown>>) => {
        if (next !== undefined) scope.set(next);
        if (wanted()) resource.reload();
        else wanted.set(true);
      },
    };
  }

  private async write(
    controller: string,
    call: Observable<ApiResult<unknown>>,
  ): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(call);
    if (result.kind === 'ok') this.tables.get(controller)?.resource.reload();
    return result;
  }
}
