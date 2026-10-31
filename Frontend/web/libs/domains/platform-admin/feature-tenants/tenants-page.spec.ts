import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import { Tenant, TenantFilters, TenantStatus } from '@harmony/platform-admin/domain';
import { SummariesStore } from '@harmony/platform-operations/data-access';
import { AccessStore } from '@harmony/platform/access';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { TenantsPage } from './tenants-page';

const TIER_TWO = 2;
const TIER_THREE = 3;
const THREE_ROWS = 3;
const SIX_HOTELS = 6;
const LICENSED = 740;
const IN_USE = 712;
const OVER_LICENSED = 120;
const OVER_IN_USE = 132;
const ACTIVE = 46;
const SUSPENDED = 2;
const TERMINATED = 1;
const TIER_CHOICES = 5;
const TIER_CELL = 2;
const HOTELS_CELL = 4;
const MODULES_CELL = 6;
const ONBOARDED_CELL = 7;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const levant: Tenant = {
  id: 't1',
  code: 'LEV',
  externalIdentifier: null,
  legalNameEn: 'Levant Hotels',
  legalNameAr: 'ليفانت للفنادق',
  countryCode: 'JO',
  defaultCurrency: 'JOD',
  status: TenantStatus.active,
  statusReason: null,
  tier: TIER_THREE,
  onboardedOn: '2026-03-12',
  isPlatform: false,
  subscriptions: [],
  hotels: SIX_HOTELS,
  roomsInUse: IN_USE,
  licensedRooms: LICENSED,
  moduleCodes: ['FO', 'HK', 'RA'],
  people: 0,
  peopleWithPassword: 0,
  twoStepExceptions: 0,
  administrator: {
    userAccountId: 'u1',
    displayName: 'Lina Nasser',
    email: 'lina.nasser@levanthotels.example',
    state: 'Active',
    invitationPending: false,
    invitationExpired: false,
    invitedOn: '2026-03-12T08:00:00Z',
    twoStepEnrolled: true,
    twoStepMethod: 'AuthenticatorApp',
    lastSignInUtc: null,
    lastSignInMethods: null,
  },
};
const bustan: Tenant = {
  ...levant,
  id: 't2',
  code: 'ABR',
  legalNameEn: 'Al Bustan Resorts',
  legalNameAr: 'منتجعات البستان',
  countryCode: 'OM',
  defaultCurrency: 'OMR',
  tier: TIER_TWO,
  hotels: 0,
  roomsInUse: 0,
  licensedRooms: 0,
  moduleCodes: [],
  people: 0,
  peopleWithPassword: 0,
  twoStepExceptions: 0,
  administrator: {
    userAccountId: 'u2',
    displayName: 'Fatma Al-Balushi',
    email: 'fatma@albustan.example',
    state: 'Active',
    invitationPending: true,
    invitationExpired: false,
    invitedOn: new Date().toISOString(),
    twoStepEnrolled: true,
    twoStepMethod: 'AuthenticatorApp',
    lastSignInUtc: null,
    lastSignInMethods: null,
  },
};
const wadi: Tenant = {
  ...levant,
  id: 't3',
  code: 'DAW',
  legalNameEn: 'Dar Al Wadi Hotel',
  legalNameAr: 'فندق دار الوادي',
  status: TenantStatus.suspended,
  statusReason: 'unpaid invoices',
  roomsInUse: OVER_IN_USE,
  licensedRooms: OVER_LICENSED,
  administrator: null,
};

class FakeStore {
  readonly filters = signal<TenantFilters>({
    search: '',
    statuses: [TenantStatus.active, TenantStatus.suspended],
    tier: null,
    countryCode: null,
    moduleCode: null,
    page: 1,
  });
  readonly rows = signal<readonly Tenant[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  searched: string[] = [];
  statuses: (readonly TenantStatus[])[] = [];
  tiers: (number | null)[] = [];
  countries: (string | null)[] = [];
  modules: (string | null)[] = [];
  search(text: string): void {
    this.searched.push(text);
  }
  showStatuses(statuses: readonly TenantStatus[]): void {
    this.statuses.push(statuses);
  }
  showTier(tier: number | null): void {
    this.tiers.push(tier);
  }
  showCountry(code: string | null): void {
    this.countries.push(code);
  }
  showModule(code: string | null): void {
    this.modules.push(code);
  }
  goTo(): void {
    return undefined;
  }
  reload(): void {
    return undefined;
  }
}

/** The nth choice in a list of inputs; the test fails plainly when it is not there. */
const pick = (list: NodeListOf<HTMLInputElement>, index: number): HTMLInputElement => {
  const box = list[index];
  if (box === undefined) throw new Error(`no choice ${String(index)}`);
  return box;
};

describe('TenantsPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<TenantsPage>;
  let page: HTMLElement;
  const summary = signal<Record<string, number> | null>(null);

  const render = async (): Promise<void> => {
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );

  beforeEach(async () => {
    store = new FakeStore();
    summary.set(null);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: TenantsStore, useValue: store },
        { provide: SummariesStore, useValue: { tenants: summary } },
        {
          provide: ReferenceStore,
          useValue: {
            choices: (controller: string) => ({
              rows: signal(
                controller === 'Country'
                  ? [{ code: 'JO', nameEn: 'Jordan', nameAr: 'الأردن' }]
                  : [{ code: 'FO', nameEn: 'Front office', nameAr: 'المكتب الأمامي' }],
              ),
              loading: signal(false),
              problem: signal(null),
              scope: signal({}),
              load: () => undefined,
            }),
          },
        },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    fixture = TestBed.createComponent(TenantsPage);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  it('lists each customer in both languages with its status, tier, country, hotels, rooms, modules, date and administrator', async () => {
    store.rows.set([levant, bustan, wadi]);
    store.total.set(THREE_ROWS);
    await render();
    expect(texts('thead th')).toEqual([
      'Tenant',
      'Status',
      'Tier',
      'Country · currency',
      'Hotels',
      'Rooms licensed · in use',
      'Modules',
      'Onboarded',
      'Administrator',
      'Actions',
    ]);
    expect(page.querySelectorAll('tbody tr')).toHaveLength(THREE_ROWS);
    const first = 'tbody tr:nth-child(1)';
    expect(texts(`${first} td.tenant .nm`)).toEqual(['LEV · Levant Hotels']);
    expect(texts(`${first} td.tenant .sub`)).toEqual(['ليفانت للفنادق']);
    expect(texts(`${first} .chip`)).toEqual(['Active']);
    expect(page.querySelector(`${first} .chip[data-s="ok"]`)).not.toBeNull();
    expect(texts(`${first} td.rooms`)).toEqual(['740 · 712']);
    expect(texts(`${first} td`).slice(TIER_CELL, HOTELS_CELL + 1)).toEqual(['3', 'JO · JOD', '6']);
    expect(texts(`${first} td`)[MODULES_CELL]).toBe('FO · HK · RA');
    expect(texts(`${first} td`)[ONBOARDED_CELL]).toBe('12 Mar 2026');
    expect(texts(`${first} td.who .nm`)).toEqual(['Lina Nasser']);
    expect(texts(`${first} td.who .sub`)).toEqual(['lina.nasser@levanthotels.example']);
    expect(page.querySelector(`${first} td.tenant a`)?.getAttribute('href')).toBe('/t1');

    const second = 'tbody tr:nth-child(2)';
    expect(texts(`${second} .chip`)).toEqual(['Onboarding']);
    expect(page.querySelector(`${second} .chip[data-s="warn"]`)).not.toBeNull();
    expect(texts(`${second} td`)[MODULES_CELL]).toBe('—');
    expect(texts(`${second} td.who .sub`)).toEqual(['invitation not accepted · 0 d']);

    const third = 'tbody tr:nth-child(3)';
    expect(texts(`${third} .chip`)).toEqual(['Suspended']);
    expect(texts(`${third} td.rooms`)).toEqual(['120 · 132 ▲ 12']);
    expect(texts(`${third} td.who .nm`)).toEqual(['no administrator']);
    expect(texts(`${third} td.who .sub`)).toEqual(['unpaid invoices']);
    expect(texts('.c-filters .count')).toEqual(['3 of 3 · by onboarded']);
  });

  it('the lead counts the customers once the summary is read', async () => {
    summary.set({ active: ACTIVE, suspended: SUSPENDED, terminated: TERMINATED });
    await render();
    expect(texts('.lead')).toEqual(['49 customers · 46 active · 2 suspended · 1 terminated']);
  });

  it('the facets and the search go to the store; the status facet opens on Active and Suspended', async () => {
    store.rows.set([levant]);
    await render();
    expect(texts('hr-facet .chip-btn')).toEqual([
      'Status: Active, Suspended',
      'Tier: any',
      'Country: any',
      'Modules: any',
    ]);
    const tier = page.querySelectorAll<HTMLInputElement>('hr-facet:nth-of-type(2) .opts input');
    expect(tier).toHaveLength(TIER_CHOICES);
    pick(tier, TIER_CELL).checked = true;
    pick(tier, TIER_CELL).dispatchEvent(new Event('change'));
    expect(store.tiers).toEqual([TIER_THREE]);
    const country = page.querySelector<HTMLInputElement>('hr-facet:nth-of-type(3) .opts input');
    if (country === null) throw new Error('no country choice');
    expect(country.value).toBe('JO');
    country.checked = true;
    country.dispatchEvent(new Event('change'));
    expect(store.countries).toEqual(['JO']);
    const status = page.querySelectorAll<HTMLInputElement>('hr-facet:nth-of-type(1) .opts input');
    pick(status, TIER_CELL).checked = true;
    pick(status, TIER_CELL).dispatchEvent(new Event('change'));
    expect(store.statuses).toEqual([
      [TenantStatus.active, TenantStatus.suspended, TenantStatus.terminated],
    ]);
    const input = page.querySelector<HTMLInputElement>('#tenants-search');
    if (input === null) throw new Error('no search field');
    input.value = 'levant';
    page.querySelector<HTMLFormElement>('form.c-filters')?.requestSubmit();
    expect(store.searched).toEqual(['levant']);
  });

  it('New tenant sits beside the title and opens the wizard; Export waits for rows; each row has its menu', async () => {
    const add = page.querySelector<HTMLAnchorElement>('.ph-acts [data-act="new-tenant"]');
    const exportButton = [...page.querySelectorAll<HTMLButtonElement>('.ph-acts button')].find(
      (button) => button.textContent.includes('Export'),
    );
    if (add === null || exportButton === undefined) throw new Error('actions missing');
    expect(add.getAttribute('href')).toBe('/tenants/new');
    expect(exportButton.disabled).toBe(true);
    store.rows.set([levant, wadi]);
    await render();
    expect(exportButton.disabled).toBe(false);
    expect(texts('tbody tr:nth-child(1) .row-menu [role="menuitem"]')).toEqual([
      'Open',
      'Edit',
      'Suspend',
      'Invite administrator',
      'Properties',
    ]);
    expect(texts('tbody tr:nth-child(2) .row-menu [role="menuitem"]')).toEqual([
      'Open',
      'Edit',
      'Reinstate',
      'Invite administrator',
      'Properties',
    ]);
    expect(
      page.querySelector('tbody tr:nth-child(1) .row-menu a:last-child')?.getAttribute('href'),
    ).toBe('/properties?tenant=t1');
  });
});
