import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Tenant, TenantDraft, TenantStatus } from '@harmony/platform-admin/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  TENANTS_PAGE_SIZE,
  TenantsStore,
  createTenantPath,
  suspendTenantPath,
  tenantsPath,
} from './tenants-store';

const ACCEPTED = 202;
const TOTAL = 3;
const SECOND_PAGE = 2;
const TIER_TWO = 2;
const AFTER_CREATE = 2;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const ritz: Tenant = {
  id: 't1',
  code: 'ritz',
  externalIdentifier: null,
  legalNameEn: 'Ritz Amman Hotels LLC',
  legalNameAr: 'فنادق ريتز عمان',
  countryCode: 'JO',
  defaultCurrency: 'JOD',
  status: TenantStatus.active,
  statusReason: null,
  tier: TIER_TWO,
  onboardedOn: '2026-10-24',
  isPlatform: false,
  subscriptions: [],
  hotels: 0,
  roomsInUse: 0,
  licensedRooms: 0,
  moduleCodes: [],
  people: 0,
  peopleWithPassword: 0,
  twoStepExceptions: 0,
  administrator: null,
};
const draft: TenantDraft = {
  code: 'bluenile',
  legalNameEn: 'Blue Nile Hotels',
  legalNameAr: 'فنادق النيل الأزرق',
  countryCode: 'JO',
  defaultCurrency: 'JOD',
  tier: 1,
  externalIdentifier: '',
  administratorEmail: 'owner@bluenile.test',
  administratorDisplayName: 'Huda Nabil',
};

describe('TenantsStore', () => {
  let store: TenantsStore;
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
    store = TestBed.inject(TenantsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('asks for the first page with no search and no status; a status filter starts at page one', async () => {
    await settle();
    const request = http.expectOne(tenantsPath);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      pageNumber: 1,
      pageSize: TENANTS_PAGE_SIZE,
      searchText: null,
      sortOrder: [{ memberName: 'CreatedDate', sortOrder: 2 }],
      statuses: ['Active', 'Suspended'],
      tier: null,
      countryCode: null,
      moduleCode: null,
    });
    request.flush({ data: { results: [ritz], totalCount: TOTAL } });
    await settle();
    expect(store.rows()).toEqual([ritz]);
    expect(store.total()).toBe(TOTAL);
    store.goTo(SECOND_PAGE);
    await settle();
    http.expectOne(tenantsPath).flush({ data: { results: [], totalCount: TOTAL } });
    store.showStatuses([TenantStatus.suspended]);
    await settle();
    expect(http.expectOne(tenantsPath).request.body).toMatchObject({
      pageNumber: 1,
      statuses: ['Suspended'],
    });
  });

  it('create sends the draft with nulls for what was not given and the key; suspend carries the reason; a refusal reads nothing', async () => {
    await settle();
    http.expectOne(tenantsPath).flush({ data: { results: [ritz], totalCount: 1 } });
    const intent = newIntentKey();
    const created = store.create(draft, intent);
    const create = http.expectOne(createTenantPath);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({
      code: 'bluenile',
      legalNameEn: 'Blue Nile Hotels',
      legalNameAr: 'فنادق النيل الأزرق',
      countryCode: 'JO',
      defaultCurrency: 'JOD',
      tier: 1,
      externalIdentifier: null,
      administratorEmail: 'owner@bluenile.test',
      administratorDisplayName: 'Huda Nabil',
    });
    expect(create.request.headers.get('Idempotency-Key')).toBe(intent.value);
    create.flush({ data: 't2' });
    expect((await created).kind).toBe('ok');
    await settle();
    http.expectOne(tenantsPath).flush({ data: { results: [ritz], totalCount: AFTER_CREATE } });

    const refused = store.suspend(ritz, 'unpaid', newIntentKey());
    const suspend = http.expectOne(suspendTenantPath);
    expect(suspend.request.body).toEqual({ id: 't1', reason: 'unpaid' });
    suspend.flush({ errors: [{ code: '00073' }] }, { status: ACCEPTED, statusText: 'Accepted' });
    expect(await refused).toEqual({ kind: 'refused', status: ACCEPTED, codes: ['00073'] });
    await settle();
    http.expectNone(tenantsPath);
  });
});
