import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from './api-client';
import { HttpStatus } from './http-status';
import { HttpStatusEvents } from './http-status-events';
import { newIntentKey } from './intent-key';
import { provideHarmonyHttp } from './provide';
import { API_RETRY_DELAYS } from './retry-policy';

const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));
const RETRIES = 2;
const ATTEMPTS = RETRIES + 1;
const busy = { status: HttpStatus.serviceUnavailable, statusText: 'Busy' };

// A W3C traceparent: "00-" + 32 hex + "-" + 16 hex + "-01".
// Matches "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"; rejects "abc".
// Tester: https://regex101.com/?regex=%5E00-%5B0-9a-f%5D%7B32%7D-%5B0-9a-f%5D%7B16%7D-01%24&testString=00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
const traceparent = /^00-[0-9a-f]{32}-[0-9a-f]{16}-01$/;
// The refusal message for an absolute URL; matches "Only relative URLs reach the BFF"; rejects "ok".
// Tester: https://regex101.com/?regex=relative&testString=Only%20relative%20URLs%20reach%20the%20BFF%0Aok
const relativeOnly = /relative/;

/** The same failed answer, `times` in a row, letting the retry timer run between them. */
const failTimes = (http: HttpTestingController, url: string, times: number): Promise<void> =>
  Array.from({ length: times }).reduce<Promise<void>>(async (previous) => {
    await previous;
    http.expectOne(url).flush(null, busy);
    await tick();
  }, Promise.resolve());

describe('ApiClient', () => {
  let client: ApiClient;
  let http: HttpTestingController;
  let events: HttpStatusEvents;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    client = TestBed.inject(ApiClient);
    http = TestBed.inject(HttpTestingController);
    events = TestBed.inject(HttpStatusEvents);
  });

  afterEach(() => {
    http.verify();
  });

  it('a 200 with data is ok; every call carries x-csrf and a traceparent', async () => {
    const answer = firstValueFrom(client.get<{ n: number }>('/api/x'));
    const req = http.expectOne('/api/x');
    expect(req.request.headers.get('x-csrf')).toBe('1');
    expect(req.request.headers.get('traceparent')).toMatch(traceparent);
    req.flush({ data: { n: 1 } });
    expect(await answer).toEqual({ kind: 'ok', status: HttpStatus.ok, data: { n: 1 } });
  });

  it('a 202 is a business refusal with its codes', async () => {
    const answer = firstValueFrom(client.post('/api/x', {}));
    http
      .expectOne('/api/x')
      .flush(
        { errors: [{ code: '10611' }] },
        { status: HttpStatus.accepted, statusText: 'Accepted' },
      );
    expect(await answer).toEqual({
      kind: 'refused',
      status: HttpStatus.accepted,
      codes: ['10611'],
    });
  });

  it('a 400 is invalid with field codes; a 403 is forbidden and counted', async () => {
    const invalid = firstValueFrom(client.post('/api/x', {}));
    http
      .expectOne('/api/x')
      .flush(
        { errors: [{ code: '10603', field: 'nameEn' }] },
        { status: HttpStatus.badRequest, statusText: 'Bad' },
      );
    expect(await invalid).toEqual({
      kind: 'invalid',
      status: HttpStatus.badRequest,
      codes: ['10603'],
    });

    const forbidden = firstValueFrom(client.get('/api/y'));
    http
      .expectOne('/api/y')
      .flush({ errors: [{ code: '000.4' }] }, { status: HttpStatus.forbidden, statusText: 'No' });
    expect(await forbidden).toEqual({
      kind: 'forbidden',
      status: HttpStatus.forbidden,
      codes: ['000.4'],
    });
    expect(events.forbidden()).toBe(1);
  });

  it('a 401 is unauthenticated and the session event counts', async () => {
    const answer = firstValueFrom(client.get('/api/x'));
    http
      .expectOne('/api/x')
      .flush(null, { status: HttpStatus.unauthorized, statusText: 'Unauthorized' });
    expect(await answer).toEqual({ kind: 'unauthenticated', status: HttpStatus.unauthorized });
    expect(events.unauthenticated()).toBe(1);
  });

  it('a GET is retried twice on 503, then succeeds', async () => {
    const answer = firstValueFrom(client.get<{ n: number }>('/api/x'));
    await failTimes(http, '/api/x', RETRIES);
    http.expectOne('/api/x').flush({ data: { n: 1 } });
    expect(await answer).toEqual({ kind: 'ok', status: HttpStatus.ok, data: { n: 1 } });
  });

  it('a GET that keeps failing ends unavailable after two retries', async () => {
    const answer = firstValueFrom(client.get('/api/x'));
    await failTimes(http, '/api/x', ATTEMPTS);
    expect(await answer).toEqual({
      kind: 'unavailable',
      status: HttpStatus.serviceUnavailable,
      codes: [],
    });
  });

  it('a write without an intent key is never retried', async () => {
    const answer = firstValueFrom(client.post('/api/x', { a: 1 }));
    const req = http.expectOne('/api/x');
    expect(req.request.headers.has('Idempotency-Key')).toBe(false);
    req.flush(null, busy);
    await tick();
    http.expectNone('/api/x');
    expect(await answer).toEqual({
      kind: 'unavailable',
      status: HttpStatus.serviceUnavailable,
      codes: [],
    });
  });

  it('a write with an intent key carries it, and retries the same key on 409 000.06', async () => {
    const intent = newIntentKey();
    const answer = firstValueFrom(client.post<{ id: string }>('/api/x', { a: 1 }, intent));
    const first = http.expectOne('/api/x');
    expect(first.request.headers.get('Idempotency-Key')).toBe(intent.value);
    first.flush(
      { errors: [{ code: '000.06' }] },
      { status: HttpStatus.conflict, statusText: 'Conflict' },
    );
    await tick();
    const second = http.expectOne('/api/x');
    expect(second.request.headers.get('Idempotency-Key')).toBe(intent.value);
    second.flush({ data: { id: 'r1' } }, { status: HttpStatus.created, statusText: 'Created' });
    expect(await answer).toEqual({ kind: 'ok', status: HttpStatus.created, data: { id: 'r1' } });
  });

  it('a 422 (key reused) is a conflict, not retried', async () => {
    const conflict = firstValueFrom(client.put('/api/x', {}, newIntentKey()));
    http
      .expectOne('/api/x')
      .flush(
        { errors: [{ code: '000.07' }] },
        { status: HttpStatus.unprocessable, statusText: 'Reused' },
      );
    expect(await conflict).toEqual({
      kind: 'conflict',
      status: HttpStatus.unprocessable,
      codes: ['000.07'],
    });
  });

  it('an absolute URL is refused before any request leaves', () => {
    expect(() => client.get('https://example.test/x')).toThrow(relativeOnly);
    expect(() => client.get('//example.test/x')).toThrow(relativeOnly);
  });
});
