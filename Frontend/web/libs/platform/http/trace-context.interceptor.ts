import { HttpInterceptorFn } from '@angular/common/http';

/** W3C Trace Context field sizes: a 16-byte trace id, an 8-byte parent (span) id. */
const TRACE_ID_BYTES = 16;
const SPAN_ID_BYTES = 8;
const HEX_RADIX = 16;
const HEX_DIGITS_PER_BYTE = 2;

const hex = (bytes: number): string =>
  Array.from(crypto.getRandomValues(new Uint8Array(bytes)), (b) =>
    b.toString(HEX_RADIX).padStart(HEX_DIGITS_PER_BYTE, '0'),
  ).join('');

/** W3C Trace Context: version 00, the trace id, the span id, sampled. */
export const newTraceparent = (): string => `00-${hex(TRACE_ID_BYTES)}-${hex(SPAN_ID_BYTES)}-01`;

/** A traceparent per request (architecture 14.2); its trace id is the correlation id every report carries. */
export const traceContextInterceptor: HttpInterceptorFn = (req, next) =>
  next(req.clone({ setHeaders: { traceparent: newTraceparent() } }));
