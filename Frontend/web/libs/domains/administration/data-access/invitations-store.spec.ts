import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Invitation } from '@harmony/administration/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  InvitationsStore,
  cancelInvitationPath,
  myInvitationsPath,
  resendInvitationPath,
} from './invitations-store';

const ACCEPTED = 202;
const waiting: Invitation = {
  id: 'i1',
  userAccountId: 'u7',
  displayName: 'Omar Haddad',
  email: 'omar@ritz.test',
  sentBy: 'Ahmad Saleh',
  sentAtUtc: '2026-10-27T08:00:00Z',
  expiresAtUtc: '2026-10-28T08:00:00Z',
  isExpired: false,
};
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

describe('InvitationsStore', () => {
  let store: InvitationsStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        InvitationsStore,
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    store = TestBed.inject(InvitationsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads the waiting invitations; resend and cancel name the account with a key and read again', async () => {
    await settle();
    http.expectOne(myInvitationsPath).flush({ data: [waiting] });
    await settle();
    expect(store.rows()).toEqual([waiting]);

    const resent = store.resend(waiting, newIntentKey());
    await settle();
    const resend = http.expectOne(resendInvitationPath);
    expect(resend.request.method).toBe('PUT');
    expect(resend.request.body).toEqual({ id: 'u7' });
    resend.flush({ data: 1 });
    expect((await resent).kind).toBe('ok');
    await settle();
    http.expectOne(myInvitationsPath).flush({ data: [waiting] });

    const cancelled = store.cancel(waiting, newIntentKey());
    await settle();
    const cancel = http.expectOne(cancelInvitationPath);
    expect(cancel.request.body).toEqual({ id: 'u7' });
    cancel.flush({ data: 1 });
    expect((await cancelled).kind).toBe('ok');
    await settle();
    http.expectOne(myInvitationsPath).flush({ data: [] });
    await settle();
    expect(store.rows()).toEqual([]);
  });

  it('a refusal reads nothing again', async () => {
    await settle();
    http.expectOne(myInvitationsPath).flush({ data: [waiting] });
    const refused = store.cancel(waiting, newIntentKey());
    await settle();
    http
      .expectOne(cancelInvitationPath)
      .flush({ errors: [{ code: '00129' }] }, { status: ACCEPTED, statusText: 'Accepted' });
    expect(await refused).toEqual({ kind: 'refused', status: ACCEPTED, codes: ['00129'] });
    await settle();
    http.expectNone(myInvitationsPath);
  });
});
