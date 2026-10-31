import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  ActorChoicesStore,
  TRAIL_PAGE_SIZE,
  TenantChoicesStore,
  TrailStore,
} from '@harmony/platform-operations/data-access';
import { TenantChoice, TrailEvent, TrailFilters } from '@harmony/platform-operations/domain';
import { SessionStore } from '@harmony/platform/session';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { TrailPage } from './trail-page';

const TOTAL = 2;
const TWO_FACTS = 2;
// Runs of whitespace (spaces, newlines) collapse to one space: matches "a \n b" (→ "a b"); rejects "ab" (nothing to do).
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const ritz: TenantChoice = {
  id: 't1',
  code: 'RITZ',
  legalNameEn: 'Ritz Amman Hotels LLC',
  legalNameAr: 'فنادق ريتز عمان',
  isPlatform: false,
};
const suspended: TrailEvent = {
  id: 'e1',
  tenantId: 'platform',
  tenantCode: 'HARMONY',
  tenantNameEn: 'Harmony',
  tenantNameAr: 'هارموني',
  userAccountId: 'u9',
  userDisplayName: 'Sara Al-Harbi',
  action: 'tenant.suspended',
  actorSubject: 'u9',
  actorDisplayName: 'Sara Al-Harbi',
  sourceAddress: '41.33.12.9',
  device: 'Edge on Windows',
  occurredAtUtc: '2026-10-28T10:42:07Z',
  detail: 'NVH',
};
const failed: TrailEvent = {
  ...suspended,
  id: 'e2',
  tenantId: 't1',
  tenantCode: 'RITZ',
  userAccountId: 'u1',
  userDisplayName: 'Amal Haddad',
  action: 'tenant.administrator-invitation-failed',
  actorSubject: 'system',
  actorDisplayName: null,
  sourceAddress: null,
  device: null,
  detail: 'RITZ',
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
  searched: string[] = [];
  actions: (string | null)[] = [];
  tenants: (string | null)[] = [];
  actors: (string | null)[] = [];
  windows: (string | null)[][] = [];
  search(text: string): void {
    this.searched.push(text);
  }
  showAction(action: string | null): void {
    this.actions.push(action);
  }
  showTenant(tenantId: string | null): void {
    this.tenants.push(tenantId);
  }
  showActor(actorSubject: string | null): void {
    this.actors.push(actorSubject);
  }
  between(from: string | null, to: string | null): void {
    this.windows.push([from, to]);
  }
  goTo(): void {
    return undefined;
  }
  reload(): void {
    return undefined;
  }
}

describe('TrailPage', () => {
  let store: FakeTrail;
  let fixture: ComponentFixture<TrailPage>;
  let page: HTMLElement;

  const render = async (): Promise<void> => {
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );

  beforeEach(async () => {
    store = new FakeTrail();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: SessionStore, useValue: { user: signal({ tenant: 'platform' }) } },
        {
          provide: TenantChoicesStore,
          useValue: {
            rows: signal([ritz]),
            truncated: signal(false),
            loading: signal(false),
            problem: signal(null),
          },
        },
        {
          provide: ActorChoicesStore,
          useValue: {
            rows: signal([
              { id: 'u9', displayName: 'Omar Haddad' },
              { id: 'u8', displayName: 'Noor Saleh' },
            ]),
            truncated: signal(false),
            loading: signal(false),
            problem: signal(null),
          },
        },
      ],
    });
    TestBed.overrideComponent(TrailPage, {
      set: { providers: [{ provide: TrailStore, useValue: store }] },
    });
    fixture = TestBed.createComponent(TrailPage);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  it('opens on the last seven days and lists the rows: time and day, code and words, the actor or the system, the customer, the target', async () => {
    expect(store.windows).toHaveLength(1);
    expect(store.windows[0]?.[0]).not.toBeNull();
    expect(store.windows[0]?.[1]).toBeNull();
    store.rows.set([suspended, failed]);
    store.total.set(TOTAL);
    await render();
    expect(texts('thead th')).toEqual(['When (UTC)', 'Event', 'Actor', 'Tenant', 'Target', 'Open']);
    const first = 'tbody tr:first-child';
    const second = 'tbody tr:last-child';
    expect(texts(`${first} td.when .nm`)).toEqual(['10:42:07']);
    expect(texts(`${first} td.event .code`)).toEqual(['tenant.suspended']);
    expect(texts(`${first} td.event .sub`)).toEqual(['Tenant suspended · NVH']);
    expect(texts(`${first} .who .nm`)).toEqual(['Sara Al-Harbi']);
    expect(texts(`${first} td.code`)).toEqual(['HARMONY']);
    expect(texts(`${first} td.target`)).toEqual(['Sara Al-Harbi']);
    expect(texts(`${second} .who .nm`)).toEqual(['system']);
    expect(texts(`${second} .who .sub`)).toEqual(['Identity service']);
    expect(page.querySelector(`${second} .c-av.none`)).not.toBeNull();
    expect(texts('.c-filters .count')).toEqual(['2 of 2']);
  });

  it('the filters go to the store: the search, the window, the customer and the family', async () => {
    const search = page.querySelector<HTMLInputElement>('#trail-search');
    if (search === null) throw new Error('no search box');
    search.value = 'NVH';
    page.querySelector('form')?.dispatchEvent(new Event('submit', { cancelable: true }));
    expect(store.searched).toEqual(['NVH']);
    const windowBox = page.querySelector<HTMLSelectElement>('#trail-window');
    if (windowBox === null) throw new Error('no window box');
    expect(windowBox.value).toBe('7d');
    windowBox.value = 'all';
    windowBox.dispatchEvent(new Event('change'));
    expect(store.windows.at(-1)).toEqual([null, null]);
    const tenantBox = page.querySelector<HTMLSelectElement>('#trail-tenant');
    if (tenantBox === null) throw new Error('no tenant box');
    expect(texts('#trail-tenant option')).toEqual(['Tenant: any', 'RITZ · Ritz Amman Hotels LLC']);
    tenantBox.value = 't1';
    tenantBox.dispatchEvent(new Event('change'));
    expect(store.tenants).toEqual(['t1']);
    const familyBox = page.querySelector<HTMLSelectElement>('#trail-family');
    if (familyBox === null) throw new Error('no family box');
    familyBox.value = 'sign-in.';
    familyBox.dispatchEvent(new Event('change'));
    expect(store.actions).toEqual(['sign-in.']);
    await render();
    expect(familyBox.closest('.sel')?.classList.contains('on')).toBe(false);
  });

  it('the actor filter lists any, the system and the platform people by name, and goes to the store', () => {
    expect(texts('#trail-actor option')).toEqual([
      'Actor: any',
      'system',
      'Noor Saleh',
      'Omar Haddad',
    ]);
    const actorBox = page.querySelector<HTMLSelectElement>('#trail-actor');
    if (actorBox === null) throw new Error('no actor box');
    actorBox.value = 'u8';
    actorBox.dispatchEvent(new Event('change'));
    expect(store.actors).toEqual(['u8']);
    actorBox.value = 'system';
    actorBox.dispatchEvent(new Event('change'));
    expect(store.actors).toEqual(['u8', 'system']);
  });

  it('a row opens in the drawer with every fact; a platform person gets an Open person link, a customer row does not', async () => {
    store.rows.set([suspended, failed]);
    store.total.set(TOTAL);
    await render();
    page.querySelector<HTMLButtonElement>('tbody tr:first-child .btn.icon')?.click();
    await render();
    expect(page.querySelector('hr-trail-event-detail')).not.toBeNull();
    expect(texts('hr-trail-event-detail .chip')).toEqual(['Tenant suspended']);
    expect(texts('hr-trail-event-detail dd').slice(0, TWO_FACTS)).toEqual([
      'Sara Al-Harbi · HARMONY',
      'HARMONY · Harmony · هارموني',
    ]);
    expect(page.querySelector('hr-trail-event-detail a[href="/people/account/u9"]')).not.toBeNull();
    page.querySelector<HTMLButtonElement>('tbody tr:last-child .btn.icon')?.click();
    await render();
    expect(page.querySelector('hr-trail-event-detail a[href="/tenants/t1"]')).not.toBeNull();
    expect(page.querySelector('hr-trail-event-detail a[href="/people/account/u1"]')).toBeNull();
  });

  it('says why when the door refused, and offers Try again; says when nothing matches; nothing to export then', async () => {
    store.problem.set('The platform did not answer.');
    await render();
    expect(texts('[role="alert"] b')).toEqual(['The platform did not answer.']);
    store.problem.set(null);
    await render();
    expect(texts('.c-empty b')).toEqual(['No events match']);
    expect(page.querySelector<HTMLButtonElement>('.ph-acts .btn')?.disabled).toBe(true);
  });
});
