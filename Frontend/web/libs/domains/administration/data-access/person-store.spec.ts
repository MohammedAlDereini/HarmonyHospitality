import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AccountState, Device, UserAccount } from '@harmony/administration/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  PersonStore,
  accountPath,
  devicesPath,
  eventsPath,
  passkeysPath,
  signOutDevicePath,
  updateRolesPath,
} from './person-store';

const ACCEPTED = 202;
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
  roleIds: ['r1'],
};
const laptop: Device = {
  id: 'd1',
  label: 'Chrome on Windows',
  firstSeenUtc: '2026-10-24T08:00:00Z',
  lastSeenUtc: '2026-10-24T09:00:00Z',
  lastAddress: '10.0.0.5',
  signedIn: true,
};

describe('PersonStore', () => {
  let store: PersonStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };
  /** Answers the four reads; the account's request may already be in hand. */
  const answerAll = (account = http.expectOne(accountPath('u5'))): void => {
    account.flush({ data: rana });
    http.expectOne(devicesPath('u5')).flush({ data: [laptop] });
    http.expectOne(passkeysPath('u5')).flush({ data: [] });
    http.expectOne(eventsPath('u5')).flush({ data: [] });
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

  it('reads nothing until a person is named; then the account, devices, passkeys and events, all GET', async () => {
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
    expect(store.problem()).toBeNull();
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
