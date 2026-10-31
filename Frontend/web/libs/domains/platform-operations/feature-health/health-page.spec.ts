import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HealthStore } from '@harmony/platform-operations/data-access';
import { HealthReport } from '@harmony/platform-operations/domain';
import { FlagsStore } from '@harmony/platform/flags';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { HealthPage } from './health-page';

/** Identity, the two hosts, the infrastructure, then the thirteen planned services. */
const MAP_CARDS = 16;
const BUILT_CARDS = 4;
// Runs of whitespace (spaces, newlines) collapse to one space: matches "a \n b" (→ "a b"); rejects "ab" (nothing to do).
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const report: HealthReport = {
  status: 'Degraded',
  checkedAtUtc: '2026-10-29T14:05:00Z',
  totalDuration: '00:00:00.1200000',
  entries: [
    {
      name: 'Identity database',
      status: 'Healthy',
      duration: '00:00:00.0120000',
      description: null,
      tags: [],
    },
    { name: 'Redis', status: 'Healthy', duration: '00:00:00.0010000', description: null, tags: [] },
    {
      name: 'Outbox',
      status: 'Degraded',
      duration: '00:00:00.0300000',
      description: '3 set aside',
      tags: [],
    },
    { name: 'Self', status: 'Healthy', duration: '00:00:00.0000100', description: null, tags: [] },
  ],
  host: {
    ring: 'all',
    release: '26.10.3',
    minimumClientVersion: '26.9.0',
    environment: 'development',
  },
};

class FakeHealth {
  readonly report = signal<HealthReport | null>(null);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  reloads = 0;
  reload(): void {
    this.reloads += 1;
  }
}

class FakeFlags {
  readonly flags = signal<Readonly<Record<string, boolean>>>({
    'night-audit': true,
    'console.billing-tab': false,
  });
  readonly ring = signal<string | null>('all');
  readonly release = signal<string | null>('26.10.3');
  readonly minimumClientVersion = signal<string | null>('26.9.0');
  reloads = 0;
  reload(): void {
    this.reloads += 1;
  }
}

describe('HealthPage', () => {
  let health: FakeHealth;
  let flags: FakeFlags;
  let fixture: ComponentFixture<HealthPage>;
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
    health = new FakeHealth();
    flags = new FakeFlags();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: HealthStore, useValue: health },
        { provide: FlagsStore, useValue: flags },
      ],
    });
    fixture = TestBed.createComponent(HealthPage);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  it('draws the map: Identity by the report, the two hosts live, the infrastructure from its checks, then the plan', async () => {
    health.report.set(report);
    await render();
    const cards = texts('.svc .svc-h b');
    expect(cards).toHaveLength(MAP_CARDS);
    expect(cards.slice(0, BUILT_CARDS)).toEqual([
      '1 · Identity',
      'Web BFF · Console BFF',
      'Infrastructure',
      '2 · Property',
    ]);
    expect(texts('.svc .chip').slice(0, BUILT_CARDS)).toEqual([
      'Attention',
      'Live',
      'Live',
      'Next',
    ]);
    expect(texts('.svc[data-state="planned"] .chip').every((word) => word === 'Planned')).toBe(
      true,
    );
    expect(texts('.c-card-h .chip')[0]).toBe('2 live');
    expect(texts('.lead')[0]).toContain('1 attention item');
  });

  it('lists every check with its word and duration, the messaging checks on their own, and the flags of the ring', async () => {
    health.report.set(report);
    await render();
    expect(texts('.c-grid section:nth-child(3) .checks li .nm')).toEqual([
      'Identity database',
      'Redis',
      'Outbox',
      'Self',
    ]);
    expect(texts('.c-grid section:nth-child(3) .checks li .sub')[0]).toBe('12 ms');
    expect(texts('.c-grid section:first-child .checks li .nm')).toEqual(['Redis', 'Outbox']);
    expect(texts('.c-grid section:first-child .checks li .chip')).toEqual(['Healthy', 'Degraded']);
    expect(texts('.flags li .nm')).toEqual(['night-audit', 'console.billing-tab']);
    expect(texts('.flags li .chip')).toEqual(['On', 'Off']);
    expect(texts('.release dd')).toEqual(['all', '26.10.3', '26.9.0', 'development']);
  });

  it('Identity not answering is said plainly; Refresh reads the health and the flags again; live updates can be paused', async () => {
    health.report.set({ ...report, status: 'Unreachable', entries: [] });
    await render();
    expect(texts('.c-msg b')).toEqual(['Identity is not answering']);
    expect(texts('.svc .chip')[0]).toBe('Not answering');
    page.querySelector<HTMLButtonElement>('.ph-acts .btn')?.click();
    expect(health.reloads).toBe(1);
    expect(flags.reloads).toBe(1);
    const live = page.querySelector<HTMLInputElement>('.c-switch input');
    expect(live?.checked).toBe(true);
    live?.click();
    await render();
    expect(live?.checked).toBe(false);
  });
});
