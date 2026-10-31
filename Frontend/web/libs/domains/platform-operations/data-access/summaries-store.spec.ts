import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PropertySummary, TenantSummary } from '@harmony/platform-operations/domain';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import { SummariesStore, propertySummaryPath, tenantSummaryPath } from './summaries-store';

const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));
const ACTIVE = 48;
const SUSPENDED = 2;
const COUNTRIES = 6;
const THIS_MONTH = 3;
const HOTELS = 112;
const ACTIVE_HOTELS = 110;
const OPENING_SOON = 4;
const NEW_HOTELS = 5;
const ROOMS = 14_320;
const FORBIDDEN = 403;

const tenants: TenantSummary = {
  today: '2026-10-29',
  active: ACTIVE,
  suspended: SUSPENDED,
  terminated: 1,
  countries: COUNTRIES,
  onboardedThisMonth: THIS_MONTH,
  onboardedByMonth: [{ month: '2026-10', count: THIS_MONTH }],
  suspendedTenants: [],
  linesEndingSoon: [],
};
const properties: PropertySummary = {
  today: '2026-10-29',
  total: HOTELS,
  active: ACTIVE_HOTELS,
  openingSoon: OPENING_SOON,
  createdThisMonth: NEW_HOTELS,
  totalRooms: ROOMS,
  licensedRooms: ROOMS,
  aboveLicence: 0,
  aboveLicenceProperties: [],
};

describe('SummariesStore', () => {
  let store: SummariesStore;
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
    store = TestBed.inject(SummariesStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads both summaries; one door down leaves the other on the page with its own problem', async () => {
    await settle();
    http.expectOne(tenantSummaryPath).flush({ data: tenants });
    http
      .expectOne(propertySummaryPath)
      .flush({ errors: [{ code: '00073' }] }, { status: FORBIDDEN, statusText: 'Forbidden' });
    await settle();
    expect(store.tenants()?.active).toBe(ACTIVE);
    expect(store.properties()).toBeNull();
    expect(store.tenantsProblem()).toBeNull();
    expect(store.propertiesProblem()).not.toBeNull();
    expect(store.loading()).toBe(false);
    store.reload();
    await settle();
    http.expectOne(tenantSummaryPath).flush({ data: tenants });
    http.expectOne(propertySummaryPath).flush({ data: properties });
    await settle();
    expect(store.properties()?.licensedRooms).toBe(ROOMS);
    expect(store.propertiesProblem()).toBeNull();
  });
});
