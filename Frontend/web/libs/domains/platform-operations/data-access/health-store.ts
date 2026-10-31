import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';
import { HealthReport } from '@harmony/platform-operations/domain';
import { messageOf } from '@harmony/platform/errors';
import { fromError } from '@harmony/platform/http';

export const healthPath = '/bff/health';

/** True when the body is the report the BFF shapes, whatever status carried it. */
const isReport = (body: unknown): body is HealthReport =>
  typeof body === 'object' &&
  body !== null &&
  typeof (body as HealthReport).status === 'string' &&
  Array.isArray((body as HealthReport).entries);

/**
 * Identity's health as the console BFF reads it inside the network (GET /bff/health). A 502 still carries a report,
 * with the word Unreachable: the page then says Identity is not answering instead of failing itself. Provided by the
 * app once: the home, the bell and the health page share one read; it reads again on demand (the Refresh button, the
 * health page's own timer).
 */
@Injectable({ providedIn: 'root' })
export class HealthStore {
  private readonly read = httpResource<HealthReport>(() => ({ url: healthPath }));

  /** The report, from a good answer or from the 502's body; null while nothing came back yet. */
  readonly report = computed<HealthReport | null>(() => {
    if (this.read.hasValue()) return this.read.value();
    const error = this.read.error();
    return error instanceof HttpErrorResponse && isReport(error.error) ? error.error : null;
  });
  readonly loading = this.read.isLoading;
  /** One sentence when the read failed for another reason (no session, the BFF itself down); null otherwise. */
  readonly problem = computed(() => {
    const error = this.read.error();
    if (error === undefined) return null;
    if (error instanceof HttpErrorResponse && isReport(error.error)) return null;
    return messageOf(fromError(error));
  });

  reload(): void {
    this.read.reload();
  }
}
