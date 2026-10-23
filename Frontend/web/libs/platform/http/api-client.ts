import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, retry, throwError, timer } from 'rxjs';
import { ApiResult, fromError, fromResponse } from './api-result';
import { isAbsoluteUrl } from './csrf.interceptor';
import { Envelope } from './envelope';
import { IntentKey, intentHeader, isFresh } from './intent-key';
import { API_RETRY_DELAYS, mayRetry, retryDelay } from './retry-policy';

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE';

/**
 * The BFF door, the only way the app talks to the platform. Relative paths only; every answer becomes an ApiResult,
 * never a thrown status; a write carries its intent key and is retried only as rule 14.1 allows.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly delays = inject(API_RETRY_DELAYS);

  get<T>(path: string): Observable<ApiResult<T>> {
    return this.send<T>('GET', path);
  }

  post<T>(path: string, body: unknown, intent?: IntentKey): Observable<ApiResult<T>> {
    return this.send<T>('POST', path, body, intent);
  }

  put<T>(path: string, body: unknown, intent?: IntentKey): Observable<ApiResult<T>> {
    return this.send<T>('PUT', path, body, intent);
  }

  delete<T>(path: string, intent?: IntentKey): Observable<ApiResult<T>> {
    return this.send<T>('DELETE', path, undefined, intent);
  }

  private send<T>(
    method: Method,
    path: string,
    body?: unknown,
    intent?: IntentKey,
  ): Observable<ApiResult<T>> {
    if (isAbsoluteUrl(path)) {
      throw new Error(`Only relative URLs reach the BFF: ${path}`);
    }
    const keyed = intent !== undefined && isFresh(intent);
    const headers: Record<string, string> = keyed ? { [intentHeader]: intent.value } : {};
    return this.http
      .request<Envelope<T>>(method, path, { body, headers, observe: 'response' })
      .pipe(
        retry({
          count: this.delays.length,
          delay: (error: unknown, attempt: number) =>
            mayRetry(method, keyed, error)
              ? timer(retryDelay(this.delays, attempt))
              : throwError(() => error),
        }),
        map((response: HttpResponse<Envelope<T>>) => fromResponse<T>(response)),
        catchError((error: unknown) => of(fromError<T>(error))),
      );
  }
}
