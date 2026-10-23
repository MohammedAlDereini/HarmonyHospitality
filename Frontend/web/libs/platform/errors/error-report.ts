import { InjectionToken } from '@angular/core';
import { ErrorKind } from './error-kind';

/**
 * What leaves the browser about a failure (architecture 14.2): kind, code, route pattern, release, browser and
 * correlation id. Never names, document numbers, card data, free text or tokens: not even the error message.
 */
export interface ErrorReport {
  readonly kind: ErrorKind;
  /** For a thrown error its class name (TypeError); for a platform outcome the platform's code. */
  readonly code: string;
  /** What the person reads out to support ("Reference 7f3a2c1d"): the first part of the correlation id. */
  readonly reference: string;
  readonly correlationId: string;
  /** The route pattern (/p/:property/front-office/check-in), never the values. */
  readonly routePattern?: string;
  readonly release: string;
  readonly browser: string;
  /** UTC, ISO 8601. */
  readonly at: string;
}

/** Where reports go. */
export interface ErrorReportSink {
  send(report: ErrorReport): void;
}

/** The browser console until platform/telemetry provides the BFF sink (D9). */
export const ERROR_REPORT_SINK = new InjectionToken<ErrorReportSink>('ERROR_REPORT_SINK', {
  providedIn: 'root',
  factory: (): ErrorReportSink => ({
    send: (report) => {
      console.error('Harmony failure report', report);
    },
  }),
});
