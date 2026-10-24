import { HttpClient } from '@angular/common/http';
import { DOCUMENT, Injectable, inject } from '@angular/core';
import { RUNTIME_CONFIG } from '@harmony/platform/config';
import { SessionStore } from '@harmony/platform/session';

/** One measurement or failure the app reports (architecture D9, ASVS 16): named, timed in UTC, never personal. */
export interface TelemetryEvent {
  readonly name: string;
  /** UTC, ISO 8601. */
  readonly at: string;
  readonly value?: number;
  /** The route pattern (/p/:property/admin), never the values. */
  readonly route?: string;
  readonly code?: string;
}

export const telemetryPath = '/bff/telemetry';
/** The BFF reads at most this many events a batch. */
export const TELEMETRY_BATCH = 20;
/** A batch waits this long for company before it goes. */
export const TELEMETRY_FLUSH_MS = 10000;
const BROWSER_LENGTH = 80;

/**
 * Collects the app's events and posts them to the BFF in batches: full, after ten seconds, or when the page is
 * being left (keepalive, so the request outlives the page). Only inside a session: the door refuses anonymity, so
 * events recorded before sign-in are dropped, never queued for a stranger.
 */
@Injectable({ providedIn: 'root' })
export class Telemetry {
  private readonly http = inject(HttpClient);
  private readonly config = inject(RUNTIME_CONFIG);
  private readonly session = inject(SessionStore);
  private readonly document = inject(DOCUMENT);
  private readonly queue: TelemetryEvent[] = [];
  private timer: ReturnType<typeof setTimeout> | null = null;
  private readonly browser = (this.document.defaultView?.navigator.userAgent ?? '').slice(
    0,
    BROWSER_LENGTH,
  );

  /**
   * Sends the queue when the page is hidden or left. Called after the vitals are watched, so these listeners run
   * after web-vitals reports its final values on the same events and the batch carries them.
   */
  flushWhenHidden(): void {
    this.document.addEventListener('visibilitychange', () => {
      if (this.document.visibilityState === 'hidden') this.flush();
    });
    this.document.defaultView?.addEventListener('pagehide', () => {
      this.flush();
    });
  }

  record(event: TelemetryEvent): void {
    this.queue.push(event);
    if (this.queue.length >= TELEMETRY_BATCH) {
      this.flush();
      return;
    }
    this.timer ??= setTimeout(() => {
      this.flush();
    }, TELEMETRY_FLUSH_MS);
  }

  /** Sends what is queued now; nothing when the queue is empty or nobody is signed in. */
  flush(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
    if (this.queue.length === 0) return;
    const events = this.queue.splice(0, TELEMETRY_BATCH);
    if (this.session.user() === null) return;
    const body = {
      events: events.map((event) => ({
        ...event,
        release: this.config.release,
        browser: this.browser,
      })),
    };
    this.http.post(telemetryPath, body, { keepalive: true }).subscribe({
      // Telemetry never disturbs the page: a lost batch is a lost batch.
      error: () => undefined,
    });
  }
}
