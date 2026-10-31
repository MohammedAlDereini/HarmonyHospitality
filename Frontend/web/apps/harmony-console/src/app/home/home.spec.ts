import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HealthStore, SummariesStore } from '@harmony/platform-operations/data-access';
import {
  HealthReport,
  PropertySummary,
  TenantSummary,
  TrailEvent,
} from '@harmony/platform-operations/domain';
import { AccessStore } from '@harmony/platform/access';
import { SessionStore } from '@harmony/platform/session';
import { AttentionItem } from '@harmony/ui/console-shell';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { AttentionStore } from '../attention-store';
import { Home } from './home';

const ACTIVE = 48;
const SUSPENDED = 2;
const COUNTRIES = 6;
const THIS_MONTH = 3;
const ROOMS = 14_320;
const DECLARED = 13_910;
const SIGNED_IN = 1204;
const REFUSED = 18;
const TWELVE = 12;
const FOUR_KPIS = 4;
const AFTERNOON = 14;
const MONTH_DIGITS = 2;
const HOTELS = 112;
const ACTIVE_HOTELS = 110;
const OPENING_SOON = 4;
const NEW_HOTELS = 5;
const YEAR = 2026;
const DAY = 29;
const MINUTE = 5;
// Runs of whitespace (spaces, newlines) collapse to one space: matches "a \n b" (→ "a b"); rejects "ab" (nothing to do).
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const months = Array.from({ length: TWELVE }, (_, index) => ({
  month: `2026-${String(index + 1).padStart(MONTH_DIGITS, '0')}`,
  count: index === TWELVE - 1 ? THIS_MONTH : 1,
}));
const tenants: TenantSummary = {
  today: '2026-12-29',
  active: ACTIVE,
  suspended: SUSPENDED,
  terminated: 1,
  countries: COUNTRIES,
  onboardedThisMonth: THIS_MONTH,
  onboardedByMonth: months,
  suspendedTenants: [],
  linesEndingSoon: [],
};
const properties: PropertySummary = {
  today: '2026-12-29',
  total: HOTELS,
  active: ACTIVE_HOTELS,
  openingSoon: OPENING_SOON,
  createdThisMonth: NEW_HOTELS,
  totalRooms: DECLARED,
  licensedRooms: ROOMS,
  aboveLicence: 1,
  aboveLicenceProperties: [],
};
const report: HealthReport = {
  status: 'Degraded',
  checkedAtUtc: '2026-12-29T14:05:00Z',
  totalDuration: '00:00:00.1',
  entries: [
    { name: 'Outbox', status: 'Degraded', duration: null, description: '3 set aside', tags: [] },
    { name: 'Redis', status: 'Healthy', duration: null, description: null, tags: [] },
  ],
  host: { ring: 'all', release: '26.10.3', minimumClientVersion: '', environment: 'development' },
};
const event: TrailEvent = {
  id: 'e1',
  tenantId: 't1',
  tenantCode: 'NVH',
  tenantNameEn: 'Nile View Hotels',
  tenantNameAr: 'فنادق إطلالة النيل',
  userAccountId: 'u1',
  userDisplayName: 'Ahmed Mansour',
  action: 'tenant.suspended',
  actorSubject: 'u9',
  actorDisplayName: 'Sara Al-Harbi',
  sourceAddress: null,
  device: null,
  occurredAtUtc: '2026-12-29T10:42:07Z',
  detail: 'NVH',
};
const needs: readonly AttentionItem[] = [
  {
    id: 'a1',
    kind: 'Suspended',
    text: 'NVH · invoice overdue',
    link: ['/tenants', 't1'],
    tone: 'stop',
  },
];

class FakeAttention {
  readonly items = signal<readonly AttentionItem[]>(needs);
  readonly recentTrail = signal<readonly TrailEvent[]>([event]);
  readonly signedInToday = signal(SIGNED_IN);
  readonly refusedToday = signal(REFUSED);
  readonly countsKnown = signal(true);
  reloads = 0;
  reload(): void {
    this.reloads += 1;
  }
}

describe('Home', () => {
  let fixture: ComponentFixture<Home>;
  let page: HTMLElement;
  let attention: FakeAttention;

  const render = async (): Promise<void> => {
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((el) =>
      el.textContent.replace(whitespace, ' ').trim(),
    );

  beforeEach(async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(YEAR, TWELVE - 1, DAY, AFTERNOON, MINUTE));
    attention = new FakeAttention();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: AttentionStore, useValue: attention },
        {
          provide: SummariesStore,
          useValue: {
            tenants: signal(tenants),
            properties: signal(properties),
            tenantsProblem: signal(null),
            propertiesProblem: signal(null),
            loading: signal(false),
          },
        },
        {
          provide: HealthStore,
          useValue: { report: signal(report), problem: signal(null), loading: signal(false) },
        },
        { provide: SessionStore, useValue: { user: signal({ name: 'Omar Haddad', tenant: 'p' }) } },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    fixture = TestBed.createComponent(Home);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('greets the person by first name for the time of day and sums the platform up in one line', () => {
    expect(texts('h1')).toEqual(['Good afternoon, Omar']);
    expect(texts('.lead')[0]).toContain('Platform operations');
    expect(texts('.lead')[0]).toContain('48 active tenants in 6 countries');
    expect(page.querySelector('.ph-acts a[href="/tenants/new"]')).not.toBeNull();
  });

  it('lists what needs a person, the four numbers, the chart, the health and the latest trail', () => {
    expect(texts('.needs li .chip')).toEqual(['Suspended']);
    expect(texts('.needs li .txt')).toEqual(['NVH · invoice overdue']);
    expect(page.querySelector('.needs li a')?.getAttribute('href')).toBe('/tenants/t1');
    expect(page.querySelectorAll('.c-kpi')).toHaveLength(FOUR_KPIS);
    expect(texts('.c-kpi .v')).toEqual(['48', '110', '14320', '1204']);
    expect(texts('.c-kpi .d')[0]).toBe('+3 this month · 2 suspended');
    expect(texts('.c-kpi .d')[2]).toBe('13910 declared · 1 hotel above licence');
    expect(texts('.c-kpi .d')[3]).toBe('18 refused · since midnight UTC');
    expect(page.querySelectorAll('svg.chart rect.bar')).toHaveLength(TWELVE);
    expect(page.querySelectorAll('svg.chart rect.bar.current')).toHaveLength(1);
    expect(texts('.chart-note')[0]).toContain('14 tenants onboarded');
    expect(texts('.lines:not(.trail) li .nm')).toEqual([
      'Identity service',
      'Web BFF · Console BFF',
      'Outbox',
      'Redis',
    ]);
    expect(texts('.lines:not(.trail) li .chip')).toEqual(['Degraded', 'Live', 'Degraded', 'Live']);
    expect(texts('.lines.trail li .nm')).toEqual(['tenant.suspended · NVH']);
    expect(texts('.lines.trail li .sub')[0]).toContain('Sara Al-Harbi');
  });

  it('Refresh reads everything again through the attention store', () => {
    // Once on arrival (what changed elsewhere shows), once more on Refresh.
    expect(attention.reloads).toBe(1);
    page.querySelector<HTMLButtonElement>('.ph-acts button')?.click();
    expect(attention.reloads).toBe(2);
  });
});
