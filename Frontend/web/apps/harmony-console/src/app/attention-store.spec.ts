import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { pendingApprovalsPath } from '@harmony/administration/data-access';
import { HealthStore, SummariesStore, trailPath } from '@harmony/platform-operations/data-access';
import {
  HealthReport,
  PropertySummary,
  TenantSummary,
  TrailEvent,
} from '@harmony/platform-operations/domain';
import { AccessStore } from '@harmony/platform/access';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import { AttentionStore } from './attention-store';

const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));
const PASSKEYS = 5;
const WRONG = 2;
const SIX_ITEMS = 6;
const TWO_READS = 2;
const ROOMS_RUN = 132;
const ROOMS_LICENSED = 120;

/** The trail request as the store sends it, for the parts the test reads. */
interface TrailBody {
  readonly action: string | null;
  readonly fromUtc: string | null;
  readonly pageSize: number;
}

const tenants: TenantSummary = {
  today: '2026-10-29',
  active: 1,
  suspended: 1,
  terminated: 0,
  countries: 1,
  onboardedThisMonth: 1,
  onboardedByMonth: [],
  suspendedTenants: [
    {
      id: 't2',
      code: 'NVH',
      legalNameEn: 'Nile View Hotels',
      legalNameAr: 'فنادق إطلالة النيل',
      statusReason: 'invoice overdue',
      modifiedDate: null,
    },
  ],
  linesEndingSoon: [
    {
      tenantId: 't1',
      tenantCode: 'LEV',
      tenantNameEn: 'Levant Hotels',
      tenantNameAr: 'ليفانت',
      propertyId: null,
      propertyCode: null,
      moduleCode: 'HK',
      endsOn: '2026-10-31',
      rooms: null,
    },
  ],
};
const properties: PropertySummary = {
  today: '2026-10-29',
  total: 1,
  active: 1,
  openingSoon: 0,
  createdThisMonth: 0,
  totalRooms: ROOMS_RUN,
  licensedRooms: ROOMS_LICENSED,
  aboveLicence: 1,
  aboveLicenceProperties: [
    {
      id: 'p1',
      tenantId: 't3',
      code: 'JED-DAW',
      nameEn: 'Dar Al Wadi',
      nameAr: 'دار الوادي',
      totalRooms: ROOMS_RUN,
      licensedRooms: ROOMS_LICENSED,
    },
  ],
};
const report: HealthReport = {
  status: 'Degraded',
  checkedAtUtc: '2026-10-29T14:05:00Z',
  totalDuration: null,
  entries: [
    { name: 'Outbox', status: 'Degraded', duration: null, description: null, tags: [] },
    { name: 'Redis', status: 'Healthy', duration: null, description: null, tags: [] },
  ],
  host: { ring: 'all', release: '26.10.3', minimumClientVersion: '', environment: 'development' },
};
const failedInvitation: TrailEvent = {
  id: 'e1',
  tenantId: 'p',
  tenantCode: 'HARMONY',
  tenantNameEn: 'Harmony',
  tenantNameAr: 'هارموني',
  userAccountId: 'u9',
  userDisplayName: 'Platform Admin',
  action: 'tenant.administrator-invitation-failed',
  actorSubject: 'system',
  actorDisplayName: null,
  sourceAddress: null,
  device: null,
  occurredAtUtc: '2026-10-29T08:03:10Z',
  detail: 'ABR',
};

describe('AttentionStore', () => {
  let store: AttentionStore;
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
        { provide: AccessStore, useValue: { can: () => true } },
        {
          provide: SummariesStore,
          useValue: {
            tenants: signal(tenants),
            properties: signal(properties),
            reload: () => undefined,
          },
        },
        { provide: HealthStore, useValue: { report: signal(report), reload: () => undefined } },
      ],
    });
    store = TestBed.inject(AttentionStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('gathers what needs a person, most pressing first, and the home’s own numbers', async () => {
    await settle();
    http.expectOne(pendingApprovalsPath).flush({ data: [{ id: 'a1' }, { id: 'a2' }] });
    const trailCalls = http.match(trailPath);
    expect(trailCalls).toHaveLength(TWO_READS);
    const bodyOf = (call: (typeof trailCalls)[number]): TrailBody => call.request.body as TrailBody;
    const failed = trailCalls.find(
      (call) => bodyOf(call).action === 'tenant.administrator-invitation-failed',
    );
    const recent = trailCalls.find((call) => bodyOf(call).action === null);
    if (failed === undefined || recent === undefined) throw new Error('the two trail reads');
    expect(bodyOf(failed).fromUtc).not.toBeNull();
    expect(bodyOf(recent).pageSize).toBe(PASSKEYS);
    failed.flush({ data: { results: [failedInvitation], totalCount: 1 } });
    recent.flush({ data: { results: [failedInvitation], totalCount: 1 } });
    const counts = http.expectOne((request) =>
      request.url.startsWith('/api/SecurityAudit/counts?fromUtc='),
    );
    counts.flush({
      data: [
        { action: 'sign-in.passkey', count: PASSKEYS },
        { action: 'sign-in.wrong-password', count: WRONG },
      ],
    });
    await settle();
    const items = store.items();
    expect(items).toHaveLength(SIX_ITEMS);
    expect(items.map((item) => item.kind)).toEqual([
      'Suspended',
      'Health',
      'Onboarding',
      'Licence',
      'Approvals',
      'Expiring',
    ]);
    expect(items[0]?.text).toBe('NVH · invoice overdue');
    expect(items[0]?.link).toEqual(['/tenants', 't2']);
    expect(items[1]?.tone).toBe('warn');
    expect(items[2]?.text).toContain('ABR');
    expect(items[3]?.text).toBe('JED-DAW · 132 rooms declared, 120 licensed');
    expect(items[4]?.text).toBe('2 waiting for a second administrator');
    expect(items[4]?.link).toEqual(['/people', 'security-rules']);
    expect(items[5]?.text).toContain('LEV · HK');
    expect(store.recentTrail()).toHaveLength(1);
    expect(store.signedInToday()).toBe(PASSKEYS);
    expect(store.refusedToday()).toBe(WRONG);
    expect(store.countsKnown()).toBe(true);
  });
});
