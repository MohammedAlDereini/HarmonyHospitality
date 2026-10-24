import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RoleOption } from '@harmony/administration/domain';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import { ROLES_PAGE_SIZE, RolesStore, rolesPath } from './roles-store';

const SERVICE_UNAVAILABLE = 503;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const frontDesk: RoleOption = { id: 'r1', code: 'FRONT-DESK', nameEn: 'Front desk', nameAr: null };

describe('RolesStore', () => {
  let store: RolesStore;
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
    store = TestBed.inject(RolesStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads nothing until asked; then one page of 100; asked again, it reads again', async () => {
    await settle();
    http.expectNone(rolesPath);
    store.load();
    await settle();
    const request = http.expectOne(rolesPath);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      pageNumber: 1,
      pageSize: ROLES_PAGE_SIZE,
      searchText: null,
    });
    request.flush({ data: { results: [frontDesk], totalCount: 1 } });
    await settle();
    expect(store.options()).toEqual([frontDesk]);
    store.load();
    await settle();
    http.expectOne(rolesPath).flush({ data: { results: [], totalCount: 0 } });
    await settle();
    expect(store.options()).toEqual([]);
  });

  it('a failed read becomes one sentence', async () => {
    store.load();
    await settle();
    http.expectOne(rolesPath).flush(null, { status: SERVICE_UNAVAILABLE, statusText: 'Down' });
    await settle();
    expect(store.problem()).toContain('not responding');
  });
});
