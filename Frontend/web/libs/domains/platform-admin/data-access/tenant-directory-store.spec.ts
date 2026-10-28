import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TenantDirectory } from '@harmony/platform-admin/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  TenantDirectoryStore,
  removeDirectoryPath,
  tenantDirectoryPath,
} from './tenant-directory-store';

const ACCEPTED = 202;
const THREE_PEOPLE = 3;
const ritz: TenantDirectory = {
  id: 'c1',
  clientId: 'a1b2',
  isEnabled: true,
  secretIssuedAtUtc: '2026-10-27T08:00:00Z',
  people: THREE_PEOPLE,
  groups: [{ id: 'g1', displayName: 'Front office', roleId: 'r1', members: THREE_PEOPLE }],
};
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

describe('TenantDirectoryStore', () => {
  let store: TenantDirectoryStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TenantDirectoryStore,
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    store = TestBed.inject(TenantDirectoryStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads nothing until a customer is named; then that customer’s directory', async () => {
    await settle();
    http.expectNone(tenantDirectoryPath('t1'));
    store.ofTenant('t1');
    await settle();
    http.expectOne(tenantDirectoryPath('t1')).flush({ data: ritz });
    await settle();
    expect(store.directory()).toEqual(ritz);
  });

  it('Remove is one PUT naming the customer with a key; success reads again, a refusal does not', async () => {
    store.ofTenant('t1');
    await settle();
    http.expectOne(tenantDirectoryPath('t1')).flush({ data: ritz });

    const removed = store.remove(newIntentKey());
    await settle();
    const put = http.expectOne(removeDirectoryPath);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ tenantId: 't1' });
    expect(put.request.headers.get('Idempotency-Key')).not.toBeNull();
    put.flush({ data: 1 });
    expect((await removed).kind).toBe('ok');
    await settle();
    http.expectOne(tenantDirectoryPath('t1')).flush({ data: null });
    await settle();
    expect(store.directory()).toBeNull();

    const refused = store.remove(newIntentKey());
    await settle();
    http
      .expectOne(removeDirectoryPath)
      .flush({ errors: [{ code: '00140' }] }, { status: ACCEPTED, statusText: 'Accepted' });
    expect(await refused).toEqual({ kind: 'refused', status: ACCEPTED, codes: ['00140'] });
    await settle();
    http.expectNone(tenantDirectoryPath('t1'));
  });
});
