import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, retry, throwError, timer } from 'rxjs';
import { ApiResult, fromError, fromPlainResponse, fromResponse } from './api-result';
import { isAbsoluteUrl } from './csrf.interceptor';
import { Envelope } from './envelope';
import { IntentKey, intentHeader, isFresh } from './intent-key';
import { API_RETRY_DELAYS, mayRetry, retryDelay } from './retry-policy';

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE';
type ToResult<B, T> = (response: HttpResponse<B>) => ApiResult<T>;

/**
 * The BFF door, the only way the app talks to the platform. Relative paths only; every answer becomes an ApiResult,
 * never a thrown status; a write carries its intent key and is retried only as rule 14.1 allows.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly delays = inject(API_RETRY_DELAYS);

  get<T>(path: string): Observable<ApiResult<T>> {
    return this.send<Envelope<T>, T>('GET', path, undefined, undefined, fromResponse);
  }

  /** A BFF door (/bff/user) answers plain JSON, not the envelope; the statuses mean the same. */
  getPlain<T>(path: string): Observable<ApiResult<T>> {
    return this.send<T, T>('GET', path, undefined, undefined, fromPlainResponse);
  }

  post<T>(path: string, body: unknown, intent?: IntentKey): Observable<ApiResult<T>> {
    return this.send<Envelope<T>, T>('POST', path, body, intent, fromResponse);
  }

  put<T>(path: string, body: unknown, intent?: IntentKey): Observable<ApiResult<T>> {
    return this.send<Envelope<T>, T>('PUT', path, body, intent, fromResponse);
  }

  delete<T>(path: string, intent?: IntentKey): Observable<ApiResult<T>> {
    return this.send<Envelope<T>, T>('DELETE', path, undefined, intent, fromResponse);
  }

  private send<B, T>(
    method: Method,
    path: string,
    body: unknown,
    intent: IntentKey | undefined,
    toResult: ToResult<B, T>,
  ): Observable<ApiResult<T>> {
    if (isAbsoluteUrl(path)) {
      throw new Error(`Only relative URLs reach the BFF: ${path}`);
    }
    const keyed = intent !== undefined && isFresh(intent);
    const headers: Record<string, string> = keyed ? { [intentHeader]: intent.value } : {};
    return this.http.request<B>(method, path, { body, headers, observe: 'response' }).pipe(
      retry({
        count: this.delays.length,
        delay: (error: unknown, attempt: number) =>
          mayRetry(method, keyed, error)
            ? timer(retryDelay(this.delays, attempt))
            : throwError(() => error),
      }),
      map(toResult),
      catchError((error: unknown) => of<ApiResult<T>>(fromError(error))),
    );
  }
}
