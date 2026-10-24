import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { ApiClient } from '@harmony/platform/http';
import { SessionStore } from '@harmony/platform/session';
import { firstValueFrom } from 'rxjs';
import { PropertyRow, PropertySummary, summaryOf } from './property-summary';

/** Identity pages its answers; a person's own hotels fit in one page of this size. */
const PAGE_SIZE = 200;
const FIRST_PAGE = 1;

export const myPropertiesPath = '/api/Property/mine';

/** The part of Identity's PagedResult the app reads. */
interface Paged<T> {
  readonly results: readonly T[];
  readonly totalCount: number;
}

export type MyPropertiesStatus = 'unknown' | 'loading' | 'ready' | 'refused' | 'unavailable';

/**
 * The person's own hotels, read once from Identity (architecture 9: "the property read"). The chooser lists them,
 * the property context finds the one in the URL, the route guard checks membership.
 */
@Injectable({ providedIn: 'root' })
export class MyProperties {
  private readonly api = inject(ApiClient);
  private readonly session = inject(SessionStore);
  private readonly listSignal = signal<readonly PropertySummary[]>([]);
  private readonly statusSignal = signal<MyPropertiesStatus>('unknown');
  private inFlight: Promise<void> | null = null;

  readonly list = this.listSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();

  constructor() {
    // A separate-window sign-in finished: what was read before may be stale.
    effect(() => {
      this.session.resumed();
      untracked(() => {
        if (this.statusSignal() !== 'unknown') void this.reload();
      });
    });
  }

  /** Reads once; later callers wait for the same read. */
  ready(): Promise<void> {
    this.inFlight ??= this.read();
    return this.inFlight;
  }

  /** Reads again, for Try again and after a resume. */
  reload(): Promise<void> {
    this.inFlight = null;
    return this.ready();
  }

  private async read(): Promise<void> {
    this.statusSignal.set('loading');
    const result = await firstValueFrom(
      this.api.post<Paged<PropertyRow>>(myPropertiesPath, {
        pageNumber: FIRST_PAGE,
        pageSize: PAGE_SIZE,
      }),
    );
    if (result.kind === 'ok') {
      this.listSignal.set(result.data.results.map(summaryOf));
      this.statusSignal.set('ready');
      return;
    }
    this.listSignal.set([]);
    this.statusSignal.set(result.kind === 'forbidden' ? 'refused' : 'unavailable');
  }
}
