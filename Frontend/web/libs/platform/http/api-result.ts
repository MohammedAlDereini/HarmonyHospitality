import { HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { codesOf, Envelope } from './envelope';
import { HttpStatus } from './http-status';

/** One of eight outcomes, decided here once; a screen never reads a status code (architecture 11.2). */
export type ApiResult<T> =
  | { kind: 'ok'; status: number; data: T }
  | { kind: 'refused'; status: number; codes: string[] } // a business rule said no (202)
  | { kind: 'invalid'; status: number; codes: string[] } // field codes (400)
  | { kind: 'unauthenticated'; status: number } // the session is gone (401)
  | { kind: 'forbidden'; status: number; codes: string[] }
  | { kind: 'busy'; status: number; codes: string[] } // 000.06: the same intent is still running
  | { kind: 'conflict'; status: number; codes: string[] } // 000.07 key reused; later a row version (D5)
  | { kind: 'unavailable'; status: number; codes: string[] }; // network, 5xx, anything unexpected

/** The receipt gate's "still running" code: the same intent, sent again too early (11.5). */
export const inFlight = '000.06';
/** The framework's code for a row saved by someone else since it was read (DbBaseContext). */
const concurrencyConflict = 'ConcurrencyConflict';
/** The receipt gate's "key reused for another intent" code (11.5). */
export const keyReused = '000.07';
/** The BFF could not be reached at all: one code for every network failure. */
export const unreachable = 'unreachable';

export function fromResponse<T>(response: HttpResponse<Envelope<T>>): ApiResult<T> {
  const codes = codesOf(response.body);
  // A row changed under the caller: Identity answers 202 with the framework's concurrency code; a conflict, not a refusal.
  if (codes.includes(concurrencyConflict)) {
    return { kind: 'conflict', status: response.status, codes };
  }
  if (response.status === HttpStatus.accepted || codes.length > 0) {
    return { kind: 'refused', status: response.status, codes };
  }
  return { kind: 'ok', status: response.status, data: response.body?.data as T };
}

/** A BFF door (/bff/user) answers the thing itself, not an envelope; its refusals still carry the statuses above. */
export function fromPlainResponse<T>(response: HttpResponse<T>): ApiResult<T> {
  return { kind: 'ok', status: response.status, data: response.body as T };
}

/** What each refusing status means; a status not listed here is "unavailable". */
const outcomeByStatus: Record<number, (codes: string[]) => ApiFailure> = {
  [HttpStatus.badRequest]: (codes) => ({ kind: 'invalid', status: HttpStatus.badRequest, codes }),
  [HttpStatus.unauthorized]: () => ({ kind: 'unauthenticated', status: HttpStatus.unauthorized }),
  [HttpStatus.forbidden]: (codes) => ({ kind: 'forbidden', status: HttpStatus.forbidden, codes }),
  [HttpStatus.conflict]: (codes) =>
    codes.includes(inFlight)
      ? { kind: 'busy', status: HttpStatus.conflict, codes }
      : { kind: 'conflict', status: HttpStatus.conflict, codes },
  [HttpStatus.unprocessable]: (codes) => ({
    kind: 'conflict',
    status: HttpStatus.unprocessable,
    codes,
  }),
  [HttpStatus.network]: () => ({
    kind: 'unavailable',
    status: HttpStatus.network,
    codes: [unreachable],
  }),
};

/** Every outcome but ok: what a thrown response becomes. */
export type ApiFailure = Exclude<ApiResult<never>, { kind: 'ok' }>;

export function fromError(error: unknown): ApiFailure {
  if (!(error instanceof HttpErrorResponse)) {
    return { kind: 'unavailable', status: HttpStatus.network, codes: [unreachable] };
  }
  const codes = codesOf(error.error);
  const outcome = outcomeByStatus[error.status];
  return outcome ? outcome(codes) : { kind: 'unavailable', status: error.status, codes };
}
