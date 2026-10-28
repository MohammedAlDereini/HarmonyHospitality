import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  DirectoryConnection,
  DirectoryGroup,
  DirectorySecret,
} from '@harmony/administration/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  DirectoryStore,
  mapDirectoryGroupPath,
  myDirectoryPath,
  newDirectorySecretPath,
  openDirectoryPath,
  switchDirectoryPath,
} from './directory-store';
import { GROUPS_PAGE_SIZE, myGroupsPath } from './person-store';

const THREE_PEOPLE = 3;
const frontOffice: DirectoryGroup = {
  id: 'g1',
  displayName: 'Front office',
  externalId: 'entra-1',
  roleId: null,
  propertyId: null,
  propertyGroupId: null,
  members: THREE_PEOPLE,
};
const connection: DirectoryConnection = {
  id: 'c1',
  clientId: 'a1b2',
  isEnabled: true,
  secretIssuedAtUtc: '2026-10-27T08:00:00Z',
  scimUrl: 'https://id.harmony.test/scim/v2',
  tokenUrl: 'https://id.harmony.test/scim/token',
  people: THREE_PEOPLE,
  groups: [frontOffice],
};
const shown: DirectorySecret = {
  clientId: 'a1b2',
  secret: 'not-a-real-secret',
  secretToken: 'a1b2.not-a-real-secret',
  scimUrl: connection.scimUrl,
  tokenUrl: connection.tokenUrl,
};
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

describe('DirectoryStore', () => {
  let store: DirectoryStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        DirectoryStore,
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    store = TestBed.inject(DirectoryStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('none connected reads as null; Open is one POST with a key; the secret is kept until forgotten', async () => {
    await settle();
    http.expectOne(myDirectoryPath).flush({ data: null });
    await settle();
    expect(store.connection()).toBeNull();
    expect(store.secret()).toBeNull();

    const opened = store.open(newIntentKey());
    await settle();
    const post = http.expectOne(openDirectoryPath);
    expect(post.request.method).toBe('POST');
    expect(post.request.headers.get('Idempotency-Key')).not.toBeNull();
    post.flush({ data: shown });
    expect((await opened).kind).toBe('ok');
    expect(store.secret()).toEqual(shown);
    await settle();
    http.expectOne(myDirectoryPath).flush({ data: connection });
    await settle();
    expect(store.groups()).toEqual([frontOffice]);
    store.forgetSecret();
    expect(store.secret()).toBeNull();
  });

  it('a new secret, the switch and a group mapping: one PUT each; success reads the connection again', async () => {
    await settle();
    http.expectOne(myDirectoryPath).flush({ data: connection });
    await settle();

    const renewed = store.newSecret(newIntentKey());
    await settle();
    const renew = http.expectOne(newDirectorySecretPath);
    expect(renew.request.method).toBe('PUT');
    renew.flush({ data: shown });
    await renewed;
    expect(store.secret()).toEqual(shown);
    await settle();
    http.expectOne(myDirectoryPath).flush({ data: connection });

    const switched = store.switch(false, newIntentKey());
    await settle();
    const off = http.expectOne(switchDirectoryPath);
    expect(off.request.body).toEqual({ on: false });
    off.flush({ data: 1 });
    await switched;
    await settle();
    http.expectOne(myDirectoryPath).flush({ data: { ...connection, isEnabled: false } });

    const mapped = store.mapGroup(
      frontOffice,
      'r1',
      { propertyId: null, propertyGroupId: 'pg1' },
      newIntentKey(),
    );
    await settle();
    const map = http.expectOne(mapDirectoryGroupPath);
    expect(map.request.body).toEqual({
      id: 'g1',
      roleId: 'r1',
      propertyId: null,
      propertyGroupId: 'pg1',
    });
    map.flush({ data: 1 });
    await mapped;
    await settle();
    http.expectOne(myDirectoryPath).flush({ data: connection });
  });

  it('the hotel groups are read only once the page asks', async () => {
    await settle();
    http.expectOne(myDirectoryPath).flush({ data: connection });
    await settle();
    http.expectNone(myGroupsPath);
    store.loadChoices();
    await settle();
    const groups = http.expectOne(myGroupsPath);
    expect(groups.request.body).toEqual({ pageNumber: 1, pageSize: GROUPS_PAGE_SIZE });
    groups.flush({ data: { results: [{ id: 'pg1', name: 'Levant' }], totalCount: 1 } });
    await settle();
    expect(store.groupChoices().length).toBe(1);
  });
});
