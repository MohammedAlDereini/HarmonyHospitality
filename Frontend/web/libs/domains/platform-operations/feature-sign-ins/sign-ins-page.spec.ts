import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  MyDevicesStore,
  TRAIL_PAGE_SIZE,
  TenantChoicesStore,
  TrailStore,
} from '@harmony/platform-operations/data-access';
import {
  MyDevice,
  TrailCount,
  TrailEvent,
  TrailFilters,
} from '@harmony/platform-operations/domain';
import { ApiResult } from '@harmony/platform/http';
import { SessionStore } from '@harmony/platform/session';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { SignInsPage } from './sign-ins-page';

const OK = 200;
const PASSKEYS_TODAY = 7;
const PASSKEYS_BEFORE = 4;
const WRONG_TODAY = 3;
const FOUR_FIGURES = 4;
// Runs of whitespace (spaces, newlines) collapse to one space: matches "a \n b" (→ "a b"); rejects "ab" (nothing to do).
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const today: readonly TrailCount[] = [
  { action: 'sign-in.passkey', count: PASSKEYS_TODAY },
  { action: 'sign-in.wrong-password', count: WRONG_TODAY },
  { action: 'sign-in.new-device-held', count: 1 },
  { action: 'device.trusted', count: 1 },
];
const before: readonly TrailCount[] = [
  { action: 'sign-in.passkey', count: PASSKEYS_BEFORE },
  { action: 'sign-in.wrong-password', count: WRONG_TODAY },
];
const passkey: TrailEvent = {
  id: 'e1',
  tenantId: 't1',
  tenantCode: 'LEV',
  tenantNameEn: 'Levant Hotels',
  tenantNameAr: 'ليفانت للفنادق',
  userAccountId: 'u1',
  userDisplayName: 'Lina Nasser',
  action: 'sign-in.passkey',
  actorSubject: 'u1',
  actorDisplayName: 'Lina Nasser',
  sourceAddress: '41.33.12.9',
  device: 'Edge on Windows',
  occurredAtUtc: '2026-10-29T12:48:52Z',
  detail: null,
};
const refused: TrailEvent = {
  ...passkey,
  id: 'e2',
  action: 'sign-in.tenant-not-active',
  userDisplayName: 'Ahmed Mansour',
  tenantCode: 'NVH',
};
const laptop: MyDevice = {
  id: 'd1',
  label: 'Edge on Windows',
  firstSeenUtc: '2026-10-01T08:00:00Z',
  lastSeenUtc: '2026-10-29T08:12:00Z',
  lastAddress: '41.33.12.9',
  signedIn: true,
  trustedAtUtc: '2026-10-01T08:00:00Z',
};

class FakeTrail {
  readonly filters = signal<TrailFilters>({
    search: '',
    action: null,
    actions: [],
    tenantId: null,
    userAccountId: null,
    actorSubject: null,
    from: null,
    to: null,
    page: 1,
  });
  readonly family = signal<string | null>(null);
  readonly pageSize = signal(TRAIL_PAGE_SIZE);
  readonly rows = signal<readonly TrailEvent[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  actionSets: (readonly string[])[] = [];
  windows: (string | null)[][] = [];
  showActions(actions: readonly string[]): void {
    this.actionSets.push(actions);
  }
  between(from: string | null, to: string | null): void {
    this.windows.push([from, to]);
  }
  /** The last day is asked for with an open end; the day before has both ends. */
  counts(_from: string | null, to: string | null): Promise<ApiResult<TrailCount[]>> {
    return Promise.resolve({ kind: 'ok', status: OK, data: [...(to === null ? today : before)] });
  }
  search(): void {
    return undefined;
  }
  showTenant(): void {
    return undefined;
  }
  goTo(): void {
    return undefined;
  }
  reload(): void {
    return undefined;
  }
}

class FakeDevices {
  readonly rows = signal<readonly MyDevice[]>([laptop]);
  readonly signedIn = signal(1);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  signedOut: string[] = [];
  signOut(device: MyDevice): Promise<ApiResult<unknown>> {
    this.signedOut.push(device.id);
    return Promise.resolve({ kind: 'ok', status: OK, data: null });
  }
  reload(): void {
    return undefined;
  }
}

describe('SignInsPage', () => {
  let store: FakeTrail;
  let devices: FakeDevices;
  let fixture: ComponentFixture<SignInsPage>;
  let page: HTMLElement;

  const render = async (): Promise<void> => {
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((el) =>
      el.textContent.replace(whitespace, ' ').trim(),
    );

  beforeEach(async () => {
    store = new FakeTrail();
    devices = new FakeDevices();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: SessionStore, useValue: { user: signal({ tenant: 'platform' }) } },
        {
          provide: TenantChoicesStore,
          useValue: {
            rows: signal([]),
            truncated: signal(false),
            loading: signal(false),
            problem: signal(null),
          },
        },
      ],
    });
    TestBed.overrideComponent(SignInsPage, {
      set: {
        providers: [
          { provide: TrailStore, useValue: store },
          { provide: MyDevicesStore, useValue: devices },
        ],
      },
    });
    fixture = TestBed.createComponent(SignInsPage);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  it('pins the sign-in family to the last day and shows the four numbers against the day before', async () => {
    expect(store.family()).toBe('sign-in.');
    expect(store.windows[0]?.[0]).not.toBeNull();
    await render();
    const figures = page.querySelectorAll('.c-kpi');
    expect(figures).toHaveLength(FOUR_FIGURES);
    expect(texts('.c-kpi .k')).toEqual([
      'Sign-ins',
      'Refused',
      'New devices held',
      'Two-step sign-ins',
    ]);
    expect(texts('.c-kpi .v')).toEqual(['7', '3', '1', '0']);
    expect(texts('.c-kpi .d')[0]).toBe('+3 vs the day before · 7 with a passkey');
    expect(texts('.c-kpi .d')[1]).toBe('same as the day before · 0 locked out');
    expect(texts('.c-kpi .d')[2]).toBe('+1 vs the day before · 1 trusted from the mail');
  });

  it('lists each sign-in with the person, the method, the place and the outcome chip', async () => {
    store.rows.set([passkey, refused]);
    await render();
    expect(texts('thead th')).toEqual([
      'When (UTC)',
      'Person',
      'Method',
      'Device · place',
      'Outcome',
      'Open',
    ]);
    expect(texts('tbody tr:first-child .who .nm')).toEqual(['Lina Nasser']);
    expect(texts('tbody tr:first-child td')[2]).toBe('Passkey');
    expect(texts('tbody tr:first-child td.place')).toEqual(['Edge on Windows · 41.33.12.9']);
    expect(texts('tbody tr:first-child .chip')).toEqual(['Signed in']);
    expect(page.querySelector('tbody tr:first-child .chip')?.getAttribute('data-s')).toBe('ok');
    expect(texts('tbody tr:last-child .chip')).toEqual(['Refused']);
    expect(page.querySelector('tbody tr:last-child .chip')?.getAttribute('data-s')).toBe('stop');
  });

  it('the outcome and the method narrow by action names; both together keep only what they share', () => {
    const outcome = page.querySelector<HTMLSelectElement>('#sign-ins-outcome');
    const method = page.querySelector<HTMLSelectElement>('#sign-ins-method');
    if (outcome === null || method === null) throw new Error('no filters');
    outcome.value = 'refused';
    outcome.dispatchEvent(new Event('change'));
    expect(store.actionSets.at(-1)).toContain('sign-in.wrong-password');
    expect(store.actionSets.at(-1)).not.toContain('sign-in.passkey');
    method.value = 'passkey';
    method.dispatchEvent(new Event('change'));
    expect(store.actionSets.at(-1)).toEqual(['sign-in.passkey-refused']);
    outcome.value = '';
    outcome.dispatchEvent(new Event('change'));
    expect(store.actionSets.at(-1)).toEqual(['sign-in.passkey', 'sign-in.passkey-refused']);
    method.value = '';
    method.dispatchEvent(new Event('change'));
    expect(store.actionSets.at(-1)).toEqual([]);
  });

  it('the rail lists the person’s browsers and signs one out', async () => {
    expect(texts('.sessions li .nm')).toEqual(['Edge on Windows']);
    expect(texts('.sessions li .sub')[0]).toContain('trusted');
    page.querySelector<HTMLButtonElement>('.sessions li .btn')?.click();
    await render();
    expect(devices.signedOut).toEqual(['d1']);
    expect(texts('.policy dt')).toEqual([
      'Two-step',
      'Session',
      'Trusted device',
      'Passwords',
      'Rate limits',
      'Tokens',
    ]);
  });
});
