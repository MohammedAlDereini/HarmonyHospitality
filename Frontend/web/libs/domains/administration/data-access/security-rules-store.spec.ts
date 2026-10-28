import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApprovalRequest, CustomerRules } from '@harmony/administration/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  SecurityRulesStore,
  approvalsRequiredPath,
  approvePath,
  inactivityDaysPath,
  myTenantPath,
  pendingApprovalsPath,
  rejectPath,
  trustedDevicesPath,
} from './security-rules-store';

const ACCEPTED = 202;
const MOST_DAYS = 90;
const FEWER_DAYS = 30;
const rules: CustomerRules = {
  approvalsRequired: true,
  inactivityDays: MOST_DAYS,
  trustedDevicesRequired: false,
};
const merge: ApprovalRequest = {
  id: 'q1',
  kind: 'account.merge',
  requestedById: 'u1',
  requestedBy: 'Ahmad Saleh',
  targetId: 'u5',
  target: 'Rana Qasem',
  raisedAtUtc: '2026-10-27T08:00:00Z',
};
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

describe('SecurityRulesStore', () => {
  let store: SecurityRulesStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };
  const answerReads = (pending: readonly ApprovalRequest[] = [merge]): void => {
    http.expectOne(myTenantPath).flush({ data: rules });
    http.expectOne(pendingApprovalsPath).flush({ data: pending });
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SecurityRulesStore,
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    store = TestBed.inject(SecurityRulesStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads the rules and the waiting requests, both GET', async () => {
    await settle();
    answerReads();
    await settle();
    expect(store.rules()).toEqual(rules);
    expect(store.waiting()).toEqual([merge]);
    expect(store.problem()).toBeNull();
  });

  it('approve, reject, approvals, days and devices: one PUT each with the key; success reads both again', async () => {
    await settle();
    answerReads();
    await settle();
    const answered = async (call: Promise<unknown>, path: string, body: unknown): Promise<void> => {
      await settle();
      const put = http.expectOne(path);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual(body);
      expect(put.request.headers.get('Idempotency-Key')).not.toBeNull();
      put.flush({ data: 1 });
      await call;
      await settle();
      answerReads([]);
      await settle();
    };
    await answered(store.approve(merge, newIntentKey()), approvePath, { id: 'q1' });
    await answered(store.reject(merge, newIntentKey()), rejectPath, { id: 'q1' });
    await answered(store.setApprovals(false, newIntentKey()), approvalsRequiredPath, {
      required: false,
    });
    await answered(store.setInactivityDays(FEWER_DAYS, newIntentKey()), inactivityDaysPath, {
      days: FEWER_DAYS,
    });
    await answered(store.setTrustedDevices(true, newIntentKey()), trustedDevicesPath, {
      required: true,
    });
    expect(store.waiting()).toEqual([]);
  });

  it('a refusal reads nothing again', async () => {
    await settle();
    answerReads();
    await settle();
    const refused = store.setApprovals(false, newIntentKey());
    await settle();
    http
      .expectOne(approvalsRequiredPath)
      .flush({ errors: [{ code: '00131' }] }, { status: ACCEPTED, statusText: 'Accepted' });
    expect(await refused).toEqual({ kind: 'refused', status: ACCEPTED, codes: ['00131'] });
    await settle();
    http.expectNone(myTenantPath);
  });
});
