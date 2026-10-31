import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PropertiesStore, ReferenceStore } from '@harmony/platform-admin/data-access';
import { Property, PropertyFilters, PropertyState } from '@harmony/platform-admin/domain';
import { SummariesStore, TenantChoicesStore } from '@harmony/platform-operations/data-access';
import { AccessStore } from '@harmony/platform/access';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { PropertiesPage } from './properties-page';

const ROOMS = 132;
const LICENSED = 120;
const TOTAL = 112;
const ACTIVE = 108;
const OPENING = 4;
const ABOVE = 2;
const TWO_ROWS = 2;
const TENANT_CELL = 1;
const TYPE_CELL = 3;
const MODULES_CELL = 5;
const SPAN_CELL = 6;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const amman: Property = {
  id: 'p1',
  tenantId: 't1',
  code: 'AMM',
  externalIdentifier: null,
  nameEn: 'Ritz Amman',
  nameAr: 'ريتز عمّان',
  propertyTypeCode: 'HOTEL',
  cityId: 'c1',
  countryCode: 'JO',
  timeZoneId: 'Asia/Amman',
  addressLine: '1 Rainbow Street',
  postalCode: null,
  defaultCurrency: 'JOD',
  defaultLanguageCode: 'en',
  totalRooms: ROOMS,
  legalEnvironmentCode: 'JO-Legal',
  taxEnvironmentCode: 'JO-Tax',
  accountingEnvironmentCode: 'JO-Accounting',
  accommodationEnvironmentCode: 'JO-Accommodation',
  opensOn: null,
  closesOn: null,
  isActive: true,
  licensedRooms: LICENSED,
  roomsAboveLicence: true,
  tenantCode: 'RITZ',
  moduleCodes: ['FO', 'HK'],
  subscriptions: [],
  contacts: [],
};
const aqaba: Property = {
  ...amman,
  id: 'p2',
  code: 'AQB',
  nameEn: 'Tala Bay Resort',
  nameAr: 'منتجع تالا باي',
  propertyTypeCode: 'RESORT',
  opensOn: '2099-12-15',
  licensedRooms: null,
  roomsAboveLicence: false,
  moduleCodes: [],
};

class FakeStore {
  readonly filters = signal<PropertyFilters>({
    tenantId: null,
    search: '',
    countryCode: null,
    state: null,
    typeCode: null,
    aboveLicence: false,
    page: 1,
  });
  readonly rows = signal<readonly Property[]>([amman, aqaba]);
  readonly total = signal(TWO_ROWS);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  tenants: (string | null)[] = [];
  searched: string[] = [];
  states: (PropertyState | null)[] = [];
  countries: (string | null)[] = [];
  types: (string | null)[] = [];
  above: boolean[] = [];
  ofTenant(id: string | null): void {
    this.tenants.push(id);
  }
  search(text: string): void {
    this.searched.push(text);
  }
  showCountry(code: string | null): void {
    this.countries.push(code);
  }
  showState(state: PropertyState | null): void {
    this.states.push(state);
  }
  showType(code: string | null): void {
    this.types.push(code);
  }
  onlyAboveLicence(flag: boolean): void {
    this.above.push(flag);
  }
  goTo(): void {
    return undefined;
  }
  reload(): void {
    return undefined;
  }
}

/** The reference tables the page reads, as the fake store answers them. */
const choiceRows: Readonly<Record<string, readonly Record<string, string>[]>> = {
  City: [{ id: 'c1', nameEn: 'Amman', nameAr: 'عمّان' }],
  PropertyType: [{ code: 'HOTEL', nameEn: 'Hotel', nameAr: 'فندق' }],
  Country: [{ code: 'JO', nameEn: 'Jordan', nameAr: 'الأردن' }],
};

describe('PropertiesPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<PropertiesPage>;
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
        { provide: PropertiesStore, useValue: store },
        { provide: SummariesStore, useValue: { properties: summary } },
        {
          provide: TenantChoicesStore,
          useValue: { rows: signal([{ id: 't1', code: 'RITZ' }]), truncated: signal(false) },
        },
        {
          provide: ReferenceStore,
          useValue: {
            choices: (controller: string) => ({
              rows: signal(choiceRows[controller] ?? []),
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
    fixture = TestBed.createComponent(PropertiesPage);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  it('lists each hotel with its customer, place, type, rooms against the licence, modules, dates and state', () => {
    expect(texts('thead th')).toEqual([
      'Hotel',
      'Tenant',
      'City · time zone',
      'Type',
      'Rooms licensed · in use',
      'Modules',
      'Opens · closes',
      'State',
      'Actions',
    ]);
    const first = 'tbody tr:nth-child(1)';
    expect(texts(`${first} td.tenant .nm`)).toEqual(['AMM · Ritz Amman']);
    expect(texts(`${first} td.tenant .sub`)).toEqual(['ريتز عمّان']);
    expect(texts(`${first} td`).slice(TENANT_CELL, TYPE_CELL + 1)).toEqual([
      'RITZ',
      'Amman · Asia/Amman',
      'Hotel',
    ]);
    expect(texts(`${first} td.rooms`)).toEqual(['120 · 132 ▲ 12']);
    expect(texts(`${first} td`)[MODULES_CELL]).toBe('FO · HK');
    expect(texts(`${first} td`)[SPAN_CELL]).toBe('— · —');
    expect(texts(`${first} .chip`)).toEqual(['Active']);
    expect(page.querySelector(`${first} .chip[data-s="ok"]`)).not.toBeNull();
    const second = 'tbody tr:nth-child(2)';
    expect(texts(`${second} td.rooms`)).toEqual(['— · 132']);
    expect(texts(`${second} td`)[TYPE_CELL]).toBe('RESORT');
    expect(texts(`${second} td`)[SPAN_CELL]).toBe('15 Dec 2099 · —');
    expect(texts(`${second} .chip`)).toEqual(['Opening']);
    expect(texts('.c-filters .count')).toEqual(['2 of 2 · by code']);
  });

  it('the lead counts the hotels once the summary is read', async () => {
    summary.set({ total: TOTAL, active: ACTIVE, openingSoon: OPENING, aboveLicence: ABOVE });
    await render();
    expect(texts('.lead')).toEqual(['112 hotels · 108 active · 4 opening soon · 2 above licence']);
  });

  it('the facets, the licence toggle and the search go to the store; ?tenant narrows the list on arrival', async () => {
    expect(store.tenants).toEqual([null]);
    expect(texts('hr-facet .chip-btn')).toEqual([
      'Tenant: any',
      'Country: any',
      'State: any',
      'Type: any',
    ]);
    const state = page.querySelectorAll<HTMLInputElement>('hr-facet:nth-of-type(3) .opts input');
    const opening = state[1];
    if (opening === undefined) throw new Error('no state choice');
    opening.checked = true;
    opening.dispatchEvent(new Event('change'));
    expect(store.states).toEqual(['Opening']);
    const above = page.querySelector<HTMLInputElement>('.c-switch input');
    if (above === null) throw new Error('no toggle');
    above.checked = true;
    above.dispatchEvent(new Event('change'));
    expect(store.above).toEqual([true]);
    const input = page.querySelector<HTMLInputElement>('#properties-search');
    if (input === null) throw new Error('no search field');
    input.value = 'tala';
    page.querySelector<HTMLFormElement>('form.c-filters')?.requestSubmit();
    expect(store.searched).toEqual(['tala']);
    fixture.componentRef.setInput('tenant', 't1');
    await render();
    expect(store.tenants).toEqual([null, 't1']);
  });

  it('New property sits beside the title and opens the editor; each row has its menu', async () => {
    const add = [...page.querySelectorAll<HTMLButtonElement>('.ph-acts button')].find((button) =>
      button.textContent.includes('New property'),
    );
    if (add === undefined) throw new Error('no New property action');
    add.click();
    await render();
    expect(page.querySelector('hr-property-editor')).not.toBeNull();
    expect(texts('tbody tr:nth-child(1) .row-menu [role="menuitem"]')).toEqual([
      'Open',
      'Tenant',
      'Subscription lines',
    ]);
  });
});
