import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MyDevice, TenantChoice } from '@harmony/platform-operations/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import { MyDevicesStore, myDevicesPath, signOutDevicePath } from './my-devices-store';
import {
  TENANT_CHOICES_PAGE_SIZE,
  TenantChoicesStore,
  tenantChoicesPath,
} from './tenant-choices-store';

const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));
const ASCENDING = 1;
const MANY = 250;

const laptop: MyDevice = {
  id: 'd1',
  label: 'Edge on Windows',
  firstSeenUtc: '2026-10-01T08:00:00Z',
  lastSeenUtc: '2026-10-29T08:12:00Z',
  lastAddress: '41.33.12.9',
  signedIn: true,
  trustedAtUtc: '2026-10-01T08:00:00Z',
};
const phone: MyDevice = {
  ...laptop,
  id: 'd2',
  label: 'Chrome on Android',
  lastSeenUtc: '2026-10-28T19:40:00Z',
  signedIn: false,
  trustedAtUtc: null,
};
const ritz: TenantChoice = {
  id: 't1',
  code: 'RITZ',
  legalNameEn: 'Ritz Amman Hotels LLC',
  legalNameAr: 'فنادق ريتز عمان',
  isPlatform: false,
};

describe('MyDevicesStore and TenantChoicesStore', () => {
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        MyDevicesStore,
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('lists the browsers most recent first, counts the ones still signed in, and signs one out with the key', async () => {
    const store = TestBed.inject(MyDevicesStore);
    await settle();
    http.expectOne(myDevicesPath).flush({ data: [phone, laptop] });
    await settle();
    expect(store.rows().map((device) => device.id)).toEqual(['d1', 'd2']);
    expect(store.signedIn()).toBe(1);
    const intent = newIntentKey();
    const done = store.signOut(laptop, intent);
    const request = http.expectOne(signOutDevicePath);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ deviceId: 'd1' });
    expect(request.request.headers.get('Idempotency-Key')).toBe(intent.value);
    request.flush({});
    expect((await done).kind).toBe('ok');
    await settle();
    http.expectOne(myDevicesPath).flush({ data: [phone, { ...laptop, signedIn: false }] });
    await settle();
    expect(store.signedIn()).toBe(0);
  });

  it('the tenant choices read one page of a hundred by code and say when there are more', async () => {
    const store = TestBed.inject(TenantChoicesStore);
    await settle();
    const request = http.expectOne(tenantChoicesPath);
    expect(request.request.body).toEqual({
      pageNumber: 1,
      pageSize: TENANT_CHOICES_PAGE_SIZE,
      sortOrder: [{ memberName: 'Code', sortOrder: ASCENDING }],
      searchText: null,
      status: null,
    });
    request.flush({ data: { results: [ritz], totalCount: MANY } });
    await settle();
    expect(store.rows()).toEqual([ritz]);
    expect(store.truncated()).toBe(true);
  });
});
