import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RUNTIME_CONFIG } from '@harmony/platform/config';
import { ERROR_REPORT_SINK } from '@harmony/platform/errors';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import { SessionStore, SessionUser } from '@harmony/platform/session';
import { TelemetryErrorSink } from './provide';
import { TELEMETRY_BATCH, Telemetry, telemetryPath } from './telemetry';

const NO_CONTENT = 204;
const LCP_MS = 1200;

const noor: SessionUser = {
  sub: 'u1',
  name: 'Noor',
  tenant: 't0',
  roles: [],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=1',
};

describe('Telemetry', () => {
  const user = signal<SessionUser | null>(noor);
  let telemetry: Telemetry;
  let http: HttpTestingController;

  beforeEach(() => {
    user.set(noor);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
        { provide: SessionStore, useValue: { user } },
        {
          provide: RUNTIME_CONFIG,
          useValue: { environment: 'test', release: '1.2.0', minimumClientVersion: '' },
        },
        TelemetryErrorSink,
        { provide: ERROR_REPORT_SINK, useExisting: TelemetryErrorSink },
      ],
    });
    telemetry = TestBed.inject(Telemetry);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('a full batch goes at once with the release, the browser, the CSRF header and keepalive; nothing while anonymous', () => {
    user.set(null);
    telemetry.record({ name: 'web.vital.lcp', at: '2026-10-24T10:00:00Z', value: LCP_MS });
    telemetry.flush();
    http.expectNone(telemetryPath);
    user.set(noor);
    Array.from({ length: TELEMETRY_BATCH }, (_, index) => index).forEach((index) => {
      telemetry.record({ name: 'web.vital.cls', at: '2026-10-24T10:00:00Z', value: index });
    });
    const request = http.expectOne(telemetryPath);
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('x-csrf')).toBe('1');
    expect(request.request.keepalive).toBe(true);
    const body = request.request.body as { events: { release: string; browser: string }[] };
    expect(body.events).toHaveLength(TELEMETRY_BATCH);
    expect(body.events[0]?.release).toBe('1.2.0');
    expect(typeof body.events[0]?.browser).toBe('string');
    request.flush(null, { status: NO_CONTENT, statusText: 'No Content' });
  });

  it('a failure report becomes one event: kind and code, the route pattern, never the message', () => {
    TestBed.inject(ERROR_REPORT_SINK).send({
      kind: 'unexpected',
      code: 'TypeError',
      reference: '7f3a2c1d',
      correlationId: '7f3a2c1d-aaaa',
      routePattern: '/p/:property/admin',
      release: '1.2.0',
      browser: 'test',
      at: '2026-10-24T10:00:00Z',
    });
    telemetry.flush();
    const request = http.expectOne(telemetryPath);
    expect(request.request.body).toEqual({
      events: [
        expect.objectContaining({
          name: 'web.error.unexpected',
          code: 'TypeError',
          route: '/p/:property/admin',
          at: '2026-10-24T10:00:00Z',
        }),
      ],
    });
    request.flush(null, { status: NO_CONTENT, statusText: 'No Content' });
  });
});
