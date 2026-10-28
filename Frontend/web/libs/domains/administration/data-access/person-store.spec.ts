import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  AccountSignIn,
  AccountState,
  CompanyLink,
  Device,
  SessionRecord,
  UserAccount,
} from '@harmony/administration/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  GROUPS_PAGE_SIZE,
  PersonStore,
  accessPath,
  accountPath,
  assignRolePath,
  devicesPath,
  endLinkPath,
  eventsPath,
  myGroupsPath,
  passkeysPath,
  passwordSignInPath,
  sessionsPath,
  signInPath,
  signOutDevicePath,
  updateRolesPath,
} from './person-store';

const ACCEPTED = 202;
const microsoftLink: CompanyLink = {
  id: 'l1',
  provider: 'Microsoft',
  linkedBy: 'first-sign-in',
  isOpen: true,
  since: '2026-10-27T08:00:00Z',
  until: null,
};
const viaCompany: AccountSignIn = {
  passwordSignIn: false,
  coveredBy: 'Microsoft',
  signsInWith: 'Company',
  links: [microsoftLink],
};
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const rana: UserAccount = {
  id: 'u5',
  userName: 'rana@ritz.test',
  email: 'rana@ritz.test',
  displayName: 'Rana Qasem',
  twoFactorEnabled: false,
  twoStepExemptReason: null,
  state: AccountState.active,
  stateReason: null,
  createdDate: '2026-10-24T08:00:00Z',
  isDirectoryManaged: false,
  roleIds: ['r1'],
};
const laptop: Device = {
  id: 'd1',
  label: 'Chrome on Windows',
  firstSeenUtc: '2026-10-24T08:00:00Z',
  lastSeenUtc: '2026-10-24T09:00:00Z',
  lastAddress: '10.0.0.5',
  signedIn: true,
  trustedAtUtc: null,
};
const webSession: SessionRecord = {
  startedAtUtc: '2026-10-24T08:00:00Z',
  endedAtUtc: null,
  endReason: null,
  methods: 'pwd,otp',
  provider: 'local',
  multiFactorSatisfied: true,
};

describe('PersonStore', () => {
  let store: PersonStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };
  /** Answers the six reads; the account's request may already be in hand. */
  const answerAll = (account = http.expectOne(accountPath('u5'))): void => {
    account.flush({ data: rana });
    http.expectOne(devicesPath('u5')).flush({ data: [laptop] });
    http.expectOne(passkeysPath('u5')).flush({ data: [] });
    http.expectOne(sessionsPath('u5')).flush({ data: [webSession] });
    http.expectOne(eventsPath('u5')).flush({ data: [] });
    http.expectOne(accessPath('u5')).flush({ data: { id: 'u5', assignments: [], grants: [] } });
    http.expectOne(signInPath('u5')).flush({ data: viaCompany });
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        PersonStore,
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    store = TestBed.inject(PersonStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads nothing until a person is named; then the account, devices, passkeys, sign-ins, events and access, all GET', async () => {
    await settle();
    http.expectNone(accountPath('u5'));
    store.open('u5');
    await settle();
    const account = http.expectOne(accountPath('u5'));
    expect(account.request.method).toBe('GET');
    answerAll(account);
    await settle();
    expect(store.person()).toEqual(rana);
    expect(store.deviceRows()).toEqual([laptop]);
    expect(store.passkeyRows()).toEqual([]);
    expect(store.sessionRows()).toEqual([webSession]);
    expect(store.assignmentRows()).toEqual([]);
    expect(store.problem()).toBeNull();
    expect(store.accessProblem()).toBeNull();
    expect(store.signInState()?.signsInWith).toBe('Company');
  });

  it('keeping a person on a password and ending a link: one PUT each with the key, then the sign-in and the trail again', async () => {
    store.open('u5');
    await settle();
    answerAll();
    await settle();
    const keep = store.setPasswordSignIn(rana, true, newIntentKey());
    await settle();
    const put = http.expectOne(passwordSignInPath);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ id: 'u5', passwordSignIn: true });
    put.flush({ data: 1 });
    expect((await keep).kind).toBe('ok');
    await settle();
    http.expectOne(signInPath('u5')).flush({ data: { ...viaCompany, passwordSignIn: true } });
    http.expectOne(eventsPath('u5')).flush({ data: [] });
    const end = store.endLink(rana, microsoftLink, newIntentKey());
    await settle();
    const endPut = http.expectOne(endLinkPath);
    expect(endPut.request.body).toEqual({ id: 'u5', linkId: 'l1' });
    endPut.flush({ data: 1 });
    expect((await end).kind).toBe('ok');
    await settle();
    http.expectOne(signInPath('u5')).flush({ data: viaCompany });
    http.expectOne(devicesPath('u5')).flush({ data: [] });
    http.expectOne(sessionsPath('u5')).flush({ data: [] });
    http.expectOne(eventsPath('u5')).flush({ data: [] });
  });

  it('the group choices are read only once an editor asks; a hotel-role write reads the access and the trail again', async () => {
    store.open('u5');
    await settle();
    answerAll();
    http.expectNone(myGroupsPath);
    store.loadChoices();
    await settle();
    const groups = http.expectOne(myGroupsPath);
    expect(groups.request.method).toBe('POST');
    expect(groups.request.body).toEqual({ pageNumber: 1, pageSize: GROUPS_PAGE_SIZE });
    groups.flush({ data: { results: [], totalCount: 0 } });
    const intent = newIntentKey();
    const done = store.assignRole(
      rana,
      'r2',
      { propertyId: 'p-amman', propertyGroupId: null },
      null,
      intent,
    );
    const request = http.expectOne(assignRolePath);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      id: 'u5',
      roleId: 'r2',
      propertyId: 'p-amman',
      propertyGroupId: null,
      endsAtUtc: null,
    });
    expect(request.request.headers.get('Idempotency-Key')).toBe(intent.value);
    request.flush({ data: 'a1' });
    expect((await done).kind).toBe('ok');
    await settle();
    http.expectOne(accessPath('u5')).flush({ data: { id: 'u5', assignments: [], grants: [] } });
    http.expectOne(eventsPath('u5')).flush({ data: [] });
    http.expectNone(accountPath('u5'));
  });

  it('a roles write carries the key and reads the account and the trail again; a refusal reads nothing', async () => {
    store.open('u5');
    await settle();
    answerAll();
    const intent = newIntentKey();
    const done = store.updateRoles(rana, ['r1', 'r2'], intent);
    const request = http.expectOne(updateRolesPath);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ id: 'u5', roleIds: ['r1', 'r2'] });
    expect(request.request.headers.get('Idempotency-Key')).toBe(intent.value);
    request.flush({ data: 1 });
    expect((await done).kind).toBe('ok');
    await settle();
    http.expectOne(accountPath('u5')).flush({ data: { ...rana, roleIds: ['r1', 'r2'] } });
    http.expectOne(eventsPath('u5')).flush({ data: [] });
    http.expectNone(devicesPath('u5'));

    const refused = store.signOutDevice(rana, laptop, newIntentKey());
    const signOut = http.expectOne(signOutDevicePath);
    expect(signOut.request.body).toEqual({ id: 'u5', deviceId: 'd1' });
    signOut.flush({ errors: [{ code: '00074' }] }, { status: ACCEPTED, statusText: 'Accepted' });
    expect(await refused).toEqual({ kind: 'refused', status: ACCEPTED, codes: ['00074'] });
    await settle();
    http.expectNone(devicesPath('u5'));
  });
});
