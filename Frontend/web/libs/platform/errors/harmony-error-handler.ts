import { ErrorHandler, Injectable, Injector, inject } from '@angular/core';
import { ActivatedRouteSnapshot, Router } from '@angular/router';
import { RUNTIME_CONFIG } from '@harmony/platform/config';
import { kindMessage } from './error-messages';
import { ERROR_REPORT_SINK, ErrorReport } from './error-report';
import { FailureNotice } from './failure-notice';

/** The same error again within this window is one report, not a storm. */
const REPEAT_WINDOW_MS = 5000;
/** At most this many reports per page life; later ones still reach the console and the notice. */
const MAX_REPORTS_PER_PAGE = 20;
/** "Reference 7f3a2c1d": how much of the correlation id a person reads out to support. */
export const REFERENCE_LENGTH = 8;

/** The matched route's pattern, segment by segment: paths from the route table, never the URL's values. */
const routePattern = (snapshot: ActivatedRouteSnapshot): string[] => [
  ...(snapshot.routeConfig?.path ? [snapshot.routeConfig.path] : []),
  ...(snapshot.firstChild ? routePattern(snapshot.firstChild) : []),
];

/**
 * Angular's last resort (architecture 14, 14.2): the full error stays in the browser console, one scrubbed report
 * leaves with a reference, the person sees the notice, the page is kept. This never throws.
 */
@Injectable()
export class HarmonyErrorHandler implements ErrorHandler {
  private readonly injector = inject(Injector);
  private readonly sink = inject(ERROR_REPORT_SINK);
  private readonly notice = inject(FailureNotice);
  private readonly config = inject(RUNTIME_CONFIG);
  private lastKey = '';
  private lastAt = 0;
  private sent = 0;

  handleError(error: unknown): void {
    try {
      console.error(error);
      const correlationId = crypto.randomUUID();
      const reference = correlationId.slice(0, REFERENCE_LENGTH);
      this.notice.show({ reference, message: kindMessage('unexpected') });
      if (this.isRepeat(error) || this.sent >= MAX_REPORTS_PER_PAGE) return;
      this.sent += 1;
      this.sink.send(this.report(error, correlationId, reference));
    } catch (failure: unknown) {
      console.error('The error handler itself failed', failure);
    }
  }

  private isRepeat(error: unknown): boolean {
    const key = error instanceof Error ? `${error.name}:${error.message}` : typeof error;
    const now = Date.now();
    const repeat = key === this.lastKey && now - this.lastAt < REPEAT_WINDOW_MS;
    this.lastKey = key;
    this.lastAt = now;
    return repeat;
  }

  private report(error: unknown, correlationId: string, reference: string): ErrorReport {
    // The router is fetched late: an error handler is built before the router exists.
    const router = this.injector.get(Router, null);
    const pattern = router === null ? '' : routePattern(router.routerState.snapshot.root).join('/');
    return {
      kind: 'unexpected',
      code: error instanceof Error ? error.name : typeof error,
      reference,
      correlationId,
      ...(pattern === '' ? {} : { routePattern: `/${pattern}` }),
      release: this.config.release,
      browser: navigator.userAgent,
      at: new Date().toISOString(),
    };
  }
}
