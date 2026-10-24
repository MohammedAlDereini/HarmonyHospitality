import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PermissionEntry, PrivilegeLevel, Role } from '@harmony/administration/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  ROLES_PAGE_SIZE,
  RolesStore,
  createRolePath,
  permissionsPath,
  rolePath,
  rolePermissionsPath,
  rolesPath,
  updateRolePath,
} from './roles-store';

const SERVICE_UNAVAILABLE = 503;
const ACCEPTED = 202;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const frontDesk: Role = {
  id: 'r1',
  code: 'FRONT-DESK',
  nameEn: 'Front desk',
  nameAr: null,
  privilegeLevel: PrivilegeLevel.standard,
  isSystemRole: false,
  isSuperRole: false,
};
const viewUsers: PermissionEntry = {
  id: 'p9',
  permissionValue: 'ViewUsers',
  name: 'View users',
  description: null,
};

describe('RolesStore', () => {
  let store: RolesStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };
  const listAnswers = (): void => {
    http.expectOne(rolesPath).flush({ data: { results: [frontDesk], totalCount: 1 } });
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
    expect(store.roles()).toEqual([frontDesk]);
    store.load();
    await settle();
    http.expectOne(rolesPath).flush({ data: { results: [], totalCount: 0 } });
    await settle();
    expect(store.roles()).toEqual([]);
  });

  it('a failed read becomes one sentence', async () => {
    store.load();
    await settle();
    http.expectOne(rolesPath).flush(null, { status: SERVICE_UNAVAILABLE, statusText: 'Down' });
    await settle();
    expect(store.problem()).toContain('not responding');
  });

  it('the catalogue is a GET, read only when asked; a detail is a GET of the role', async () => {
    store.loadCatalogue();
    await settle();
    const catalogue = http.expectOne(permissionsPath);
    expect(catalogue.request.method).toBe('GET');
    catalogue.flush({ data: [viewUsers] });
    await settle();
    expect(store.permissions()).toEqual([viewUsers]);
    const detail = store.detail(frontDesk);
    const request = http.expectOne(rolePath('r1'));
    expect(request.request.method).toBe('GET');
    request.flush({ data: { ...frontDesk, permissions: [viewUsers] } });
    const result = await detail;
    expect(result.kind).toBe('ok');
    if (result.kind === 'ok') expect(result.data.permissions).toEqual([viewUsers]);
  });

  it('create, update, permissions and remove carry the intent key and read the list again on success', async () => {
    store.load();
    await settle();
    listAnswers();
    const draft = {
      code: 'NIGHT',
      nameEn: 'Night audit',
      nameAr: '',
      privilegeLevel: PrivilegeLevel.standard,
    };
    const intent = newIntentKey();

    const created = store.create(draft, ['p9'], intent);
    const create = http.expectOne(createRolePath);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({
      code: 'NIGHT',
      nameEn: 'Night audit',
      nameAr: null,
      privilegeLevel: 'Standard',
      permissionIds: ['p9'],
    });
    expect(create.request.headers.get('Idempotency-Key')).toBe(intent.value);
    create.flush({ data: 'r2' });
    expect((await created).kind).toBe('ok');
    await settle();
    listAnswers();

    const updated = store.update(frontDesk, { ...draft, nameAr: 'تدقيق ليلي' }, newIntentKey());
    const update = http.expectOne(updateRolePath);
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toMatchObject({ id: 'r1', nameAr: 'تدقيق ليلي' });
    update.flush({ data: 1 });
    expect((await updated).kind).toBe('ok');
    await settle();
    listAnswers();

    const granted = store.setPermissions(frontDesk, ['p9'], newIntentKey());
    const grant = http.expectOne(rolePermissionsPath);
    expect(grant.request.body).toEqual({ roleId: 'r1', permissionIds: ['p9'] });
    grant.flush({ data: 1 });
    expect((await granted).kind).toBe('ok');
    await settle();
    listAnswers();

    const refused = store.remove(frontDesk, newIntentKey());
    const remove = http.expectOne(rolePath('r1'));
    expect(remove.request.method).toBe('DELETE');
    remove.flush({ errors: [{ code: '00012' }] }, { status: ACCEPTED, statusText: 'Accepted' });
    expect(await refused).toEqual({ kind: 'refused', status: ACCEPTED, codes: ['00012'] });
    await settle();
    http.expectNone(rolesPath);
  });
});
