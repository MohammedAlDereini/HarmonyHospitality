import { HttpErrorResponse } from '@angular/common/http';
import { InjectionToken } from '@angular/core';
import { inFlight } from './api-result';
import { codesOf } from './envelope';
import { HttpStatus } from './http-status';

/** Architecture 14.1: the waits before the first and the second retry, plus a little jitter. */
const FIRST_RETRY_MS = 500;
const SECOND_RETRY_MS = 1500;
const JITTER_MS = 250;

/** The waits before each retry; tests shorten them to zero. */
export const API_RETRY_DELAYS = new InjectionToken<readonly number[]>('API_RETRY_DELAYS', {
  providedIn: 'root',
  factory: () => [FIRST_RETRY_MS, SECOND_RETRY_MS],
});

/** A GET is retried on these: the server was not reached, or a gateway in front of it gave up. */
const retryableReadStatuses: readonly number[] = [
  HttpStatus.network,
  HttpStatus.badGateway,
  HttpStatus.serviceUnavailable,
  HttpStatus.gatewayTimeout,
];

/**
 * 14.1: a GET is retried on network errors, 502, 503 and 504; a write with an intent key on network errors, 503 and
 * the "still running" answer (409 000.06), with the same key; a write without a key is never retried.
 */
export function mayRetry(method: string, keyed: boolean, error: unknown): boolean {
  if (!(error instanceof HttpErrorResponse)) return false;
  if (method === 'GET') return retryableReadStatuses.includes(error.status);
  if (!keyed) return false;
  return (
    error.status === HttpStatus.network ||
    error.status === HttpStatus.serviceUnavailable ||
    (error.status === HttpStatus.conflict && codesOf(error.error).includes(inFlight))
  );
}

/** The wait before retry number `attempt` (1-based), with jitter when there is a wait at all. */
export function retryDelay(delays: readonly number[], attempt: number): number {
  const base = delays[attempt - 1] ?? delays[delays.length - 1] ?? 0;
  return base === 0 ? 0 : base + Math.floor(Math.random() * JITTER_MS);
}
