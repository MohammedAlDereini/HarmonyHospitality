import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AccountState, UserAccount } from '@harmony/administration/domain';
import { PropertyScope } from '@harmony/platform/context';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  USERS_PAGE_SIZE,
  UsersStore,
  createPath,
  reinstatePath,
  suspendPath,
  terminatePath,
  usersPath,
} from './users-store';

const SERVICE_UNAVAILABLE = 503;
const ACCEPTED = 202;
const SECOND_PAGE = 2;
const TOTAL = 40;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const ahmad: UserAccount = {
  id: 'u1',
  userName: 'ahmad@ritz.test',
  email: 'ahmad@ritz.test',
  displayName: 'Ahmad Saleh',
  twoFactorEnabled: true,
  twoStepExemptReason: null,
  state: AccountState.active,
  stateReason: null,
  createdDate: '2026-10-24T08:00:00Z',
  roleIds: ['r1'],
};

describe('UsersStore', () => {
  let store: UsersStore;
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
    store = TestBed.inject(UsersStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('asks Identity for the first page with no search, and keeps the rows and the total', async () => {
    await settle();
    const request = http.expectOne(usersPath);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      pageNumber: 1,
      pageSize: USERS_PAGE_SIZE,
      searchText: null,
    });
    request.flush({ data: { results: [ahmad], totalCount: TOTAL } });
    await settle();
    expect(store.rows()).toEqual([ahmad]);
    expect(store.total()).toBe(TOTAL);
    expect(store.problem()).toBeNull();
  });

  it('a search starts at page one; a page change keeps the search', async () => {
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [], totalCount: 0 } });
    store.goTo(SECOND_PAGE);
    await settle();
    expect(http.expectOne(usersPath).request.body).toMatchObject({ pageNumber: SECOND_PAGE });
    store.search('  ahmad ');
    await settle();
    expect(http.expectOne(usersPath).request.body).toEqual({
      pageNumber: 1,
      pageSize: USERS_PAGE_SIZE,
      searchText: 'ahmad',
    });
  });

  it('a failed read becomes one sentence; reload asks again', async () => {
    await settle();
    http.expectOne(usersPath).flush(null, { status: SERVICE_UNAVAILABLE, statusText: 'Down' });
    await settle();
    expect(store.problem()).toContain('not responding');
    store.reload();
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [ahmad], totalCount: 1 } });
    await settle();
    expect(store.problem()).toBeNull();
    expect(store.rows()).toHaveLength(1);
  });

  it('a property switch resets the filters', async () => {
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [], totalCount: 0 } });
    store.search('x');
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [], totalCount: 0 } });
    TestBed.inject(PropertyScope).reset();
    expect(store.filters()).toEqual({ search: '', page: 1 });
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [], totalCount: 0 } });
  });
  it('suspend sends the reason with the intent key, then reads the list again; a refusal comes back as it is', async () => {
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [ahmad], totalCount: 1 } });
    const intent = newIntentKey();
    const done = store.suspend(ahmad, 'left without notice', intent);
    const request = http.expectOne(suspendPath);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ id: 'u1', reason: 'left without notice' });
    expect(request.request.headers.get('Idempotency-Key')).toBe(intent.value);
    request.flush({ data: 1 });
    expect((await done).kind).toBe('ok');
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [ahmad], totalCount: 1 } });

    const refused = store.reinstate(ahmad, newIntentKey());
    http
      .expectOne(reinstatePath)
      .flush({ errors: [{ code: '00076' }] }, { status: ACCEPTED, statusText: 'Accepted' });
    expect(await refused).toEqual({ kind: 'refused', status: ACCEPTED, codes: ['00076'] });
    await settle();
    http.expectNone(usersPath);
  });

  it('terminate sends the reason with the intent key to the termination door', async () => {
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [ahmad], totalCount: 1 } });
    const intent = newIntentKey();
    const done = store.terminate(ahmad, 'contract ended', intent);
    const request = http.expectOne(terminatePath);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ id: 'u1', reason: 'contract ended' });
    expect(request.request.headers.get('Idempotency-Key')).toBe(intent.value);
    request.flush({ data: 1 });
    expect((await done).kind).toBe('ok');
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [ahmad], totalCount: 1 } });
  });

  it('create posts the name, the email and the one role with the intent key, no password; then reads the list again', async () => {
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [ahmad], totalCount: 1 } });
    const intent = newIntentKey();
    const done = store.create(
      { displayName: 'Rana Qasem', email: 'rana@ritz.test', roleId: 'r1' },
      intent,
    );
    const request = http.expectOne(createPath);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      displayName: 'Rana Qasem',
      email: 'rana@ritz.test',
      roleIds: ['r1'],
    });
    expect(request.request.headers.get('Idempotency-Key')).toBe(intent.value);
    request.flush({ data: 'u9' });
    expect((await done).kind).toBe('ok');
    await settle();
    http.expectOne(usersPath).flush({ data: { results: [ahmad], totalCount: 1 } });
  });
});
