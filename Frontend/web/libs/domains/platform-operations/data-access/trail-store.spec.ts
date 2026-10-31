import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TrailEvent } from '@harmony/platform-operations/domain';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import { TRAIL_PAGE_SIZE, TrailStore, trailCountsPath, trailPath } from './trail-store';

const TOTAL = 42;
const DESCENDING = 2;
const SECOND_PAGE = 2;
const OK = 200;
const FORBIDDEN = 403;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const row: TrailEvent = {
  id: 'e1',
  tenantId: 't1',
  tenantCode: 'RITZ',
  tenantNameEn: 'Ritz Amman Hotels LLC',
  tenantNameAr: 'فنادق ريتز عمان',
  userAccountId: 'u1',
  userDisplayName: 'Amal Haddad',
  action: 'sign-in.passkey',
  actorSubject: 'u1',
  actorDisplayName: 'Amal Haddad',
  sourceAddress: '::1',
  device: 'Chrome on Windows',
  occurredAtUtc: '2026-10-28T10:42:07Z',
  detail: null,
};

describe('TrailStore', () => {
  let store: TrailStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TrailStore,
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    store = TestBed.inject(TrailStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('asks for the first page newest first with every filter open, and reads the rows and the total', async () => {
    await settle();
    const request = http.expectOne(trailPath);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      pageNumber: 1,
      pageSize: TRAIL_PAGE_SIZE,
      sortOrder: [{ memberName: 'OccurredAtUtc', sortOrder: DESCENDING }],
      searchText: null,
      action: null,
      actions: null,
      tenantId: null,
      userAccountId: null,
      actorSubject: null,
      fromUtc: null,
      toUtc: null,
    });
    request.flush({ data: { results: [row], totalCount: TOTAL } });
    await settle();
    expect(store.rows()).toEqual([row]);
    expect(store.total()).toBe(TOTAL);
    expect(store.problem()).toBeNull();
  });

  it('a pinned family is the action until the person narrows inside it; the filters stack and start at page one', async () => {
    await settle();
    http.expectOne(trailPath).flush({ data: { results: [], totalCount: 0 } });
    store.family.set('sign-in.');
    await settle();
    expect(http.expectOne(trailPath).request.body).toMatchObject({ action: 'sign-in.' });
    store.goTo(SECOND_PAGE);
    await settle();
    expect(http.expectOne(trailPath).request.body).toMatchObject({ pageNumber: SECOND_PAGE });
    store.showAction('sign-in.passkey');
    await settle();
    expect(http.expectOne(trailPath).request.body).toMatchObject({
      pageNumber: 1,
      action: 'sign-in.passkey',
    });
    store.showActions(['sign-in.wrong-password', 'sign-in.locked-out']);
    store.showTenant('t1');
    store.showPerson('u1');
    store.showActor('system');
    store.between('2026-10-28T00:00:00.000Z', '2026-10-29T00:00:00.000Z');
    store.search('  ritz ');
    await settle();
    expect(http.expectOne(trailPath).request.body).toMatchObject({
      pageNumber: 1,
      searchText: 'ritz',
      actions: ['sign-in.wrong-password', 'sign-in.locked-out'],
      tenantId: 't1',
      userAccountId: 'u1',
      actorSubject: 'system',
      fromUtc: '2026-10-28T00:00:00.000Z',
      toUtc: '2026-10-29T00:00:00.000Z',
    });
  });

  it('counts asks the counts door with only the parts given, in the query string', async () => {
    await settle();
    http.expectOne(trailPath).flush({ data: { results: [], totalCount: 0 } });
    const all = store.counts(null, null, null);
    http.expectOne(trailCountsPath).flush({ data: [{ action: 'sign-in.passkey', count: 1 }] });
    expect(await all).toEqual({
      kind: 'ok',
      status: OK,
      data: [{ action: 'sign-in.passkey', count: 1 }],
    });
    const query = new URLSearchParams({ fromUtc: '2026-10-28T00:00:00.000Z', action: 'sign-in.' });
    const some = store.counts('2026-10-28T00:00:00.000Z', null, 'sign-in.');
    http.expectOne(`${trailCountsPath}?${query.toString()}`).flush({ data: [] });
    expect((await some).kind).toBe('ok');
  });

  it('a door that refuses leaves the rows empty and a sentence in problem', async () => {
    await settle();
    http
      .expectOne(trailPath)
      .flush({ errors: [{ code: '00073' }] }, { status: FORBIDDEN, statusText: 'Forbidden' });
    await settle();
    expect(store.rows()).toEqual([]);
    expect(store.problem()).not.toBeNull();
  });
});
