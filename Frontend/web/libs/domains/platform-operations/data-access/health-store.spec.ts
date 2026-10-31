import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { HealthReport } from '@harmony/platform-operations/domain';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import { HealthStore, healthPath } from './health-store';

const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));
const BAD_GATEWAY = 502;
const UNAUTHORIZED = 401;
const TWO_CHECKS = 2;

const report: HealthReport = {
  status: 'Degraded',
  checkedAtUtc: '2026-10-29T14:05:00Z',
  totalDuration: '00:00:00.1200000',
  entries: [
    { name: 'Self', status: 'Healthy', duration: '00:00:00.0001', description: null, tags: [] },
    {
      name: 'Outbox',
      status: 'Degraded',
      duration: '00:00:00.0300',
      description: '3 set aside',
      tags: [],
    },
  ],
  host: { ring: 'all', release: '26.10.3', minimumClientVersion: '', environment: 'development' },
};
const unreachable: HealthReport = { ...report, status: 'Unreachable', entries: [] };

describe('HealthStore', () => {
  let store: HealthStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    store = TestBed.inject(HealthStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads the report; a 502 that still carries a report is Identity not answering, not a failure of the page', async () => {
    await settle();
    http.expectOne(healthPath).flush(report);
    await settle();
    expect(store.report()?.status).toBe('Degraded');
    expect(store.report()?.entries).toHaveLength(TWO_CHECKS);
    expect(store.problem()).toBeNull();
    store.reload();
    await settle();
    http
      .expectOne(healthPath)
      .flush(unreachable, { status: BAD_GATEWAY, statusText: 'Bad Gateway' });
    await settle();
    expect(store.report()?.status).toBe('Unreachable');
    expect(store.problem()).toBeNull();
  });

  it('a refusal without a report is a problem and no report', async () => {
    await settle();
    http.expectOne(healthPath).flush(null, { status: UNAUTHORIZED, statusText: 'Unauthorized' });
    await settle();
    expect(store.report()).toBeNull();
    expect(store.problem()).not.toBeNull();
  });
});
