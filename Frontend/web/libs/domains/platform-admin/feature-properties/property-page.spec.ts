import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { GroupsStore, PropertiesStore, ReferenceStore } from '@harmony/platform-admin/data-access';
import {
  Property,
  PropertyGroup,
  PropertyUpdate,
  SubscriptionLine,
} from '@harmony/platform-admin/domain';
import { TenantChoicesStore, TrailStore } from '@harmony/platform-operations/data-access';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult } from '@harmony/platform/http';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { PropertyPage } from './property-page';

const OK = 200;
const ROOMS = 132;
const LICENSED = 120;
const OVER = 12;
const TERMINALS = 6;
const PRECEDENCE = 10;
const THREE_FACTS_BEFORE_SPAN = 3;
const NEW_ROOMS = 140;
const SECOND_READ = 2;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const frontOffice: SubscriptionLine = {
  id: 'l1abcdef-rest',
  tenantId: 't1',
  propertyId: 'p1',
  moduleCode: 'FO',
  isEnabled: true,
  disabledReason: null,
  renewsAutomatically: true,
  rooms: LICENSED,
  terminals: TERMINALS,
  startsOn: '2026-10-24',
  endsOn: null,
  isHeld: true,
};
const epos: SubscriptionLine = {
  ...frontOffice,
  id: 'l2',
  propertyId: null,
  moduleCode: 'EPOS',
  rooms: null,
  terminals: null,
  renewsAutomatically: false,
  endsOn: '2027-01-01',
};
const amman: Property = {
  id: 'p1',
  tenantId: 't1',
  code: 'RITZ-AMM',
  externalIdentifier: null,
  nameEn: 'Ritz Amman',
  nameAr: 'ريتز عمان',
  propertyTypeCode: 'HOTEL',
  cityId: 'c1',
  countryCode: 'JO',
  timeZoneId: 'Asia/Amman',
  addressLine: '12 Zahran Street',
  postalCode: '11183',
  defaultCurrency: 'JOD',
  defaultLanguageCode: 'en',
  totalRooms: ROOMS,
  legalEnvironmentCode: 'JO',
  taxEnvironmentCode: 'JO-GST',
  accountingEnvironmentCode: 'IFRS',
  accommodationEnvironmentCode: 'JO-HOTEL',
  opensOn: '2024-05-01',
  closesOn: null,
  isActive: true,
  licensedRooms: LICENSED,
  roomsAboveLicence: true,
  tenantCode: 'ritz',
  moduleCodes: ['FO', 'EPOS'],
  subscriptions: [frontOffice, epos],
  contacts: [
    {
      id: 'k1',
      name: 'Rami Haddad',
      role: 'General manager',
      email: 'rami@ritz.test',
      telephone: null,
      isPrimary: true,
    },
  ],
};
const brand: PropertyGroup = {
  id: 'g1',
  tenantId: 't1',
  typeCode: 'BRAND',
  code: 'RITZ',
  externalIdentifier: null,
  nameEn: 'Ritz Hotels',
  nameAr: 'فنادق ريتز',
  description: null,
  precedence: PRECEDENCE,
  displayOrder: 1,
  isActive: true,
  currentMembers: 1,
  brand: {
    themeReference: null,
    logoFileId: null,
    websiteUrl: null,
    franchisorName: null,
    agreementStartsOn: null,
    agreementEndsOn: null,
    royaltyPercent: null,
  },
  members: [
    {
      id: 'm1',
      propertyId: 'p1',
      propertyCode: 'RITZ-AMM',
      propertyNameEn: 'Ritz Amman',
      propertyNameAr: 'ريتز عمان',
      fromUtc: '2026-03-12T08:00:00Z',
      toUtc: null,
      isCurrent: true,
    },
    {
      id: 'm0',
      propertyId: 'p1',
      propertyCode: 'RITZ-AMM',
      propertyNameEn: 'Ritz Amman',
      propertyNameAr: 'ريتز عمان',
      fromUtc: '2025-01-01T00:00:00Z',
      toUtc: '2026-03-12T08:00:00Z',
      isCurrent: false,
    },
  ],
};
const referenceRows: Readonly<Record<string, readonly Record<string, string>[]>> = {
  City: [{ id: 'c1', nameEn: 'Amman', nameAr: 'عمّان' }],
  PropertyType: [{ code: 'HOTEL', nameEn: 'Hotel', nameAr: 'فندق' }],
  Module: [
    { code: 'FO', nameEn: 'Front office', nameAr: 'المكتب الأمامي' },
    { code: 'EPOS', nameEn: 'Point of sale', nameAr: 'نقاط البيع' },
  ],
  Language: [{ code: 'en', nameEn: 'English', nameAr: 'الإنجليزية' }],
};

class FakeStore {
  property: Property = amman;
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  updates: PropertyUpdate[] = [];
  reads = 0;
  detail(): Promise<ApiResult<Property>> {
    this.reads += 1;
    return Promise.resolve({ kind: 'ok', status: OK, data: this.property });
  }
  update(_property: Property, update: PropertyUpdate): Promise<ApiResult<unknown>> {
    this.updates.push(update);
    return Promise.resolve(this.answer);
  }
}

describe('PropertyPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<PropertyPage>;
  let page: HTMLElement;
  const tenantsOfGroups: (string | null)[] = [];
  const tenantsOfTrail: (string | null)[] = [];
  const searchesOfTrail: string[] = [];

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const click = (selector: string): void => {
    page.querySelector<HTMLElement>(selector)?.click();
  };
  const text = (selector: string): string =>
    page.querySelector(selector)?.textContent.replace(whitespace, ' ').trim() ?? '';
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );
  const type = (selector: string, value: string): void => {
    const box = page.querySelector<HTMLInputElement>(selector);
    if (box === null) throw new Error(`no ${selector}`);
    box.value = value;
    box.dispatchEvent(new Event('input'));
  };

  const mount = async (tab?: string): Promise<void> => {
    fixture = TestBed.createComponent(PropertyPage);
    fixture.componentRef.setInput('id', 'p1');
    if (tab !== undefined) fixture.componentRef.setInput('tab', tab);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  };

  beforeEach(() => {
    store = new FakeStore();
    tenantsOfGroups.length = 0;
    tenantsOfTrail.length = 0;
    searchesOfTrail.length = 0;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: PropertiesStore, useValue: store },
        {
          provide: GroupsStore,
          useValue: {
            rows: signal([brand]),
            ofTenant: (id: string | null) => {
              tenantsOfGroups.push(id);
            },
          },
        },
        {
          provide: ReferenceStore,
          useValue: {
            choices: (controller: string) => ({
              rows: signal(referenceRows[controller] ?? []),
              loading: signal(false),
              problem: signal(null),
              scope: signal({}),
              load: () => undefined,
            }),
          },
        },
        {
          provide: TenantChoicesStore,
          useValue: {
            rows: signal([
              {
                id: 't1',
                code: 'ritz',
                legalNameEn: 'Ritz Amman Hotels LLC',
                legalNameAr: 'فنادق ريتز عمان',
                isPlatform: false,
              },
            ]),
          },
        },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    TestBed.overrideComponent(PropertyPage, {
      set: {
        providers: [
          {
            provide: TrailStore,
            useValue: {
              rows: signal([]),
              problem: signal(null),
              pageSize: signal(0),
              showTenant: (id: string | null) => {
                tenantsOfTrail.push(id);
              },
              search: (text: string) => {
                searchesOfTrail.push(text);
              },
            },
          },
        ],
      },
    });
  });

  it('reads the hotel; the head names it with its code, state and the licence gap; the lead says whose it is and where', async () => {
    await mount();
    expect(store.reads).toBe(1);
    expect(tenantsOfGroups).toEqual(['t1']);
    expect(tenantsOfTrail).toEqual(['t1']);
    expect(searchesOfTrail).toEqual(['RITZ-AMM']);
    expect(text('h1')).toContain('Ritz Amman');
    expect(text('.code-tile')).toBe('RITZ-AMM');
    expect(text('[data-status]')).toBe('Active');
    expect(texts('.ph-title .chip')).toEqual(['Active', `${String(OVER)} rooms above licence`]);
    expect(text('.lead')).toBe(
      'Ritz Amman Hotels LLC (ritz) · Hotel · Amman · Asia/Amman · JOD · English · 12 Zahran Street · 11183',
    );
    expect(texts('.c-tabs [role="tab"]')).toEqual([
      'Overview',
      'Subscriptions · 2',
      'Contacts · 1',
      'Groups · 1',
      'Trail',
    ]);
  });

  it('says the licence gap in a message bar that points at the customer page, pre-aimed at this hotel', async () => {
    await mount();
    expect(text('.c-msg b')).toBe(`${String(OVER)} rooms above the licence.`);
    const raise = page.querySelector<HTMLAnchorElement>('.c-msg a.btn');
    expect(raise?.textContent.trim()).toBe('Raise the line');
    expect(raise?.getAttribute('href')).toBe('/tenants/t1?tab=subscriptions&subscribe=p1');
  });

  it('the overview cards say the licence, the contacts and the groups', async () => {
    await mount();
    const facts = texts('.cards .c-facts dd');
    expect(facts.slice(0, THREE_FACTS_BEFORE_SPAN)).toEqual([
      String(LICENSED),
      `${String(ROOMS)} ▲ ${String(OVER)}`,
      `${String(TERMINALS)} licensed`,
    ]);
    expect(facts).toContain('01 May 2024 · —');
    expect(facts).toContain('JO · JO-GST · IFRS · JO-HOTEL');
    expect(facts).toContain('General manager · primary rami@ritz.test');
    expect(facts).toContain('BRAND · since 12 Mar 2026');
    expect(facts).toContain('Ritz Hotels');
  });

  it('lists the lines with their scope, state, period and the rooms the hotel has above the line', async () => {
    await mount();
    const rows = [...page.querySelectorAll('tr.lrow')].map((row) =>
      [...row.querySelectorAll('td')].map((cell) =>
        cell.textContent.replace(whitespace, ' ').trim(),
      ),
    );
    expect(rows).toEqual([
      [
        'FO · Front office line l1abcdef',
        'This hotel',
        'Active',
        '24 Oct 2026 → open',
        `${String(LICENSED)} ▲ ${String(OVER)} in use`,
        String(TERMINALS),
        'Renews',
      ],
      [
        'EPOS · Point of sale chain-wide line',
        'Whole tenant',
        'Ends 1 Jan',
        '24 Oct 2026 → 01 Jan 2027',
        '—',
        '—',
        'Fixed term',
      ],
    ]);
  });

  it('opens on the tab the route asks for; the groups tab shows every membership, past ones too', async () => {
    await mount('groups');
    expect(page.querySelector('[role="tab"][aria-selected="true"]')?.textContent.trim()).toBe(
      'Groups · 1',
    );
    expect(texts('table.data tbody tr td:last-child .chip')).toEqual(['Current', 'Past']);
    click('[role="tab"]:nth-child(3)');
    await settle();
    expect(texts('tr.crow td')).toEqual([
      'Rami Haddad',
      'General manager',
      'rami@ritz.test',
      '',
      'Primary',
    ]);
  });

  it('edits in the drawer and saves the trimmed values', async () => {
    await mount();
    click('[data-act="edit"]');
    await settle();
    expect(page.querySelector<HTMLInputElement>('#edit-name-en')?.value).toBe('Ritz Amman');
    type('#edit-name-en', ' Ritz Amman Downtown ');
    type('#edit-rooms', ` ${String(NEW_ROOMS)} `);
    await settle();
    page.querySelector<HTMLFormElement>('form.editor')?.requestSubmit();
    await settle();
    expect(store.updates).toEqual([
      {
        nameEn: 'Ritz Amman Downtown',
        nameAr: 'ريتز عمان',
        addressLine: '12 Zahran Street',
        postalCode: '11183',
        totalRooms: NEW_ROOMS,
        opensOn: '2024-05-01',
        closesOn: null,
      },
    ]);
    expect(store.reads).toBe(SECOND_READ);
  });
});
