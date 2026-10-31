import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import {
  ACTOR_CHOICES_PAGE_SIZE,
  ActorChoicesStore,
  actorChoicesPath,
} from './actor-choices-store';

const FORBIDDEN = 403;
const A_BIG_TEAM = 240;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));
const noor = { id: 'u8', displayName: 'Noor Saleh' };

describe('ActorChoicesStore', () => {
  let store: ActorChoicesStore;
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
    store = TestBed.inject(ActorChoicesStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads the first hundred people of the platform in one page and says when there are more', async () => {
    await settle();
    const request = http.expectOne(actorChoicesPath);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      pageNumber: 1,
      pageSize: ACTOR_CHOICES_PAGE_SIZE,
      searchText: null,
    });
    request.flush({ data: { results: [noor], totalCount: A_BIG_TEAM } });
    await settle();
    expect(store.rows()).toEqual([noor]);
    expect(store.truncated()).toBe(true);
    expect(store.problem()).toBeNull();
  });

  it('a door that refuses leaves the list empty and a sentence in problem', async () => {
    await settle();
    http
      .expectOne(actorChoicesPath)
      .flush({ errors: [{ code: '000.03' }] }, { status: FORBIDDEN, statusText: 'Forbidden' });
    await settle();
    expect(store.rows()).toEqual([]);
    expect(store.truncated()).toBe(false);
    expect(store.problem()).not.toBeNull();
  });
});
