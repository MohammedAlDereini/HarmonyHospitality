import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import { SessionStore } from '@harmony/platform/session';
import { MyProperties, myPropertiesPath } from './my-properties';

const FORBIDDEN = 403;
const SERVICE_UNAVAILABLE = 503;
const EXPECTED_PAGE_SIZE = 100;
/** A field Identity sends that no screen reads; it must not survive the mapping. */
const ROOMS_NOBODY_READS = 120;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const grandRow = {
  code: 'GRD',
  nameEn: 'Grand Riyadh',
  nameAr: 'غراند الرياض',
  timeZoneId: 'Asia/Riyadh',
  defaultCurrency: 'SAR',
  totalRooms: ROOMS_NOBODY_READS,
};

describe('MyProperties', () => {
  let properties: MyProperties;
  let http: HttpTestingController;
  const resumed = signal(0);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
        { provide: SessionStore, useValue: { resumed, user: signal(null) } },
      ],
    });
    properties = TestBed.inject(MyProperties);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads the first page of my hotels once and keeps only what screens need', async () => {
    expect(properties.status()).toBe('unknown');
    const ready = properties.ready();
    void properties.ready();
    const request = http.expectOne(myPropertiesPath);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ pageNumber: 1, pageSize: EXPECTED_PAGE_SIZE });
    request.flush({ data: { results: [grandRow], totalCount: 1 } });
    await ready;
    expect(properties.status()).toBe('ready');
    expect(properties.list()).toEqual([
      {
        code: 'GRD',
        nameEn: 'Grand Riyadh',
        nameAr: 'غراند الرياض',
        timeZoneId: 'Asia/Riyadh',
        currency: 'SAR',
      },
    ]);
  });

  it('a 403 (no ViewProperties) is refused: an empty list, nothing else', async () => {
    const ready = properties.ready();
    http
      .expectOne(myPropertiesPath)
      .flush({ errors: [{ code: '000.4' }] }, { status: FORBIDDEN, statusText: 'No' });
    await ready;
    expect(properties.status()).toBe('refused');
    expect(properties.list()).toEqual([]);
  });

  it('the service not answering is unavailable; reload reads again', async () => {
    const ready = properties.ready();
    http
      .expectOne(myPropertiesPath)
      .flush(null, { status: SERVICE_UNAVAILABLE, statusText: 'Down' });
    await ready;
    expect(properties.status()).toBe('unavailable');
    const again = properties.reload();
    http.expectOne(myPropertiesPath).flush({ data: { results: [], totalCount: 0 } });
    await again;
    expect(properties.status()).toBe('ready');
  });

  it('after a separate-window sign-in finished, the list is read again', async () => {
    const ready = properties.ready();
    http.expectOne(myPropertiesPath).flush({ data: { results: [], totalCount: 0 } });
    await ready;
    resumed.update((n) => n + 1);
    TestBed.tick();
    await tick();
    http.expectOne(myPropertiesPath).flush({ data: { results: [grandRow], totalCount: 1 } });
    await tick();
    expect(properties.list()).toHaveLength(1);
  });
});
