import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { GroupsStore, ReferenceStore } from '@harmony/platform-admin/data-access';
import { GroupUpdate, Property, PropertyGroup } from '@harmony/platform-admin/domain';
import { TenantChoicesStore } from '@harmony/platform-operations/data-access';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult } from '@harmony/platform/http';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { GroupPage } from './group-page';

const OK = 200;
const PRECEDENCE = 100;
const ROYALTY = 4.5;
const ROOMS = 120;
const THREE = 3;
const SECOND_READ = 2;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const amman: Property = {
  id: 'p1',
  tenantId: 't1',
  code: 'AMM-GRD',
  externalIdentifier: null,
  nameEn: 'Grand Amman Hotel',
  nameAr: 'فندق عمان الكبير',
  propertyTypeCode: 'HOTEL',
  cityId: 'c1',
  countryCode: 'JO',
  timeZoneId: 'Asia/Amman',
  addressLine: '',
  postalCode: null,
  defaultCurrency: 'JOD',
  defaultLanguageCode: 'en',
  totalRooms: ROOMS,
  legalEnvironmentCode: 'JO',
  taxEnvironmentCode: 'JO',
  accountingEnvironmentCode: 'IFRS',
  accommodationEnvironmentCode: 'JO',
  opensOn: null,
  closesOn: null,
  isActive: true,
  licensedRooms: null,
  roomsAboveLicence: false,
  tenantCode: 'LEV',
  moduleCodes: [],
  subscriptions: [],
  contacts: [],
};
const aqaba: Property = {
  ...amman,
  id: 'p2',
  code: 'AQB-TAL',
  nameEn: 'Tala Bay Resort',
  cityId: 'c2',
};
const levant: PropertyGroup = {
  id: 'g1',
  tenantId: 't1',
  typeCode: 'Brand',
  code: 'LEVANT-BRAND',
  externalIdentifier: 'LEV-BR-01',
  nameEn: 'Levant Hotels',
  nameAr: 'فنادق المشرق',
  description: 'The group’s flagship brand.',
  precedence: PRECEDENCE,
  displayOrder: 1,
  isActive: true,
  currentMembers: 1,
  brand: {
    themeReference: 'levant-classic',
    logoFileId: null,
    websiteUrl: 'levanthotels.example',
    franchisorName: 'Levant Hospitality Holdings',
    agreementStartsOn: '2024-01-01',
    agreementEndsOn: '2028-12-31',
    royaltyPercent: ROYALTY,
  },
  members: [
    {
      id: 'm1',
      propertyId: 'p1',
      propertyCode: 'AMM-GRD',
      propertyNameEn: 'Grand Amman Hotel',
      propertyNameAr: 'فندق عمان الكبير',
      fromUtc: '2024-01-01T00:00:00Z',
      toUtc: null,
      isCurrent: true,
    },
    {
      id: 'm0',
      propertyId: 'p3',
      propertyCode: 'AMM-CTY',
      propertyNameEn: 'Levant City Suites',
      propertyNameAr: 'ليفانت سيتي سويتس',
      fromUtc: '2024-01-01T00:00:00Z',
      toUtc: '2025-02-28T00:00:00Z',
      isCurrent: false,
    },
  ],
};
const referenceRows: Readonly<Record<string, readonly Record<string, string>[]>> = {
  City: [
    { id: 'c1', nameEn: 'Amman', nameAr: 'عمّان' },
    { id: 'c2', nameEn: 'Aqaba', nameAr: 'العقبة' },
  ],
  PropertyGroupType: [{ code: 'Brand', nameEn: 'Brand', nameAr: 'علامة' }],
};

class FakeStore {
  group: PropertyGroup = levant;
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  reads = 0;
  hotelsAsked: string[] = [];
  updates: GroupUpdate[] = [];
  joined: string[] = [];
  ended: string[] = [];
  detail(): Promise<ApiResult<PropertyGroup>> {
    this.reads += 1;
    return Promise.resolve({ kind: 'ok', status: OK, data: this.group });
  }
  hotelsOf(tenantId: string): Promise<readonly Property[]> {
    this.hotelsAsked.push(tenantId);
    return Promise.resolve([amman, aqaba]);
  }
  update(_group: PropertyGroup, update: GroupUpdate): Promise<ApiResult<unknown>> {
    this.updates.push(update);
    return Promise.resolve(this.answer);
  }
  addMember(groupId: string, propertyId: string): Promise<ApiResult<unknown>> {
    this.joined.push(`${groupId}:${propertyId}`);
    return Promise.resolve(this.answer);
  }
  endMembership(memberId: string): Promise<ApiResult<unknown>> {
    this.ended.push(memberId);
    return Promise.resolve(this.answer);
  }
}

describe('GroupPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<GroupPage>;
  let page: HTMLElement;

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
  const mount = async (tab?: string): Promise<void> => {
    fixture = TestBed.createComponent(GroupPage);
    fixture.componentRef.setInput('id', 'g1');
    if (tab !== undefined) fixture.componentRef.setInput('tab', tab);
    page = fixture.nativeElement as HTMLElement;
    await settle();
    await settle();
  };

  beforeEach(() => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: GroupsStore, useValue: store },
        {
          provide: TenantChoicesStore,
          useValue: {
            rows: signal([
              {
                id: 't1',
                code: 'LEV',
                legalNameEn: 'Levant Hotels Group',
                legalNameAr: 'مجموعة فنادق المشرق',
                isPlatform: false,
              },
            ]),
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
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
  });

  it('reads the group and the customer’s hotels; the head names it with its code, state and type; the lead says whose it is', async () => {
    await mount();
    expect(store.reads).toBe(1);
    expect(store.hotelsAsked).toEqual(['t1']);
    expect(text('h1')).toContain('Levant Hotels');
    expect(text('.code-tile')).toBe('LEVANT-BRAND');
    expect(text('[data-status]')).toBe('Active');
    expect(texts('.ph-title .chip')).toEqual(['Active', 'Brand']);
    expect(text('.lead')).toBe(
      'Levant Hotels Group (LEV) · precedence 100 · 1 hotel now, 1 past · agreement 01 Jan 2024 – 31 Dec 2028 · royalty 4.5 %',
    );
    expect(texts('.c-tabs [role="tab"]')).toEqual([
      'Overview',
      'Hotels · 1',
      'Brand',
      'Standards · proposed',
    ]);
  });

  it('the overview holds the brand, the about and the proposed standards; the hotels table says city, period and state', async () => {
    await mount();
    expect(texts('.cards .c-card-h h3')).toEqual(['Brand', 'About', 'Standards · proposed']);
    expect(texts('.cards .c-facts dd')).toEqual([
      'Levant Hospitality Holdings',
      'levanthotels.example',
      'levant-classic',
      '01 Jan 2024 – 31 Dec 2028',
      '4.5 %',
      'LEVANT-BRAND',
      'LEV-BR-01',
      '1',
    ]);
    expect(text('.proposed .chip')).toBe('Not built');
    const rows = [...page.querySelectorAll('tr.mrow')].map((row) =>
      [...row.querySelectorAll('td')].map((cell) =>
        cell.textContent.replace(whitespace, ' ').trim(),
      ),
    );
    expect(rows).toEqual([
      [
        'AMM-GRD · Grand Amman Hotel فندق عمان الكبير',
        'Amman',
        '01 Jan 2024',
        '—',
        'Current',
        'End',
      ],
      [
        'AMM-CTY · Levant City Suites ليفانت سيتي سويتس',
        '',
        '01 Jan 2024',
        '28 Feb 2025',
        'Past',
        '',
      ],
    ]);
    expect(page.querySelector('tr.mrow a.nm')?.getAttribute('href')).toBe('/properties/p1');
  });

  it('opens on the tab the route asks for', async () => {
    await mount('hotels');
    expect(page.querySelector('[role="tab"][aria-selected="true"]')?.textContent.trim()).toBe(
      'Hotels · 1',
    );
    expect(page.querySelectorAll('.cards').length).toBe(0);
    expect(page.querySelectorAll('tr.mrow').length).toBe(SECOND_READ);
  });

  it('Add hotel offers only the hotels not in the group and joins the chosen one', async () => {
    await mount();
    click('.ph-acts [data-act="join"]');
    await settle();
    const box = page.querySelector<HTMLSelectElement>('#join-hotel');
    if (box === null) throw new Error('no hotel box');
    expect([...box.options].map((option) => option.textContent.trim())).toEqual([
      'Choose a hotel',
      'AQB-TAL · Tala Bay Resort',
    ]);
    box.value = 'p2';
    box.dispatchEvent(new Event('change'));
    await settle();
    page.querySelector<HTMLFormElement>('#join-hotel')?.closest('form')?.requestSubmit();
    await settle();
    expect(store.joined).toEqual(['g1:p2']);
    expect(store.reads).toBe(SECOND_READ);
  });

  it('edits in the drawer and saves the brand with the group', async () => {
    await mount();
    click('[data-act="edit"]');
    await settle();
    const name = page.querySelector<HTMLInputElement>('#edit-name-en');
    if (name === null) throw new Error('no name field');
    expect(name.value).toBe('Levant Hotels');
    name.value = ' Levant Hotels & Resorts ';
    name.dispatchEvent(new Event('input'));
    await settle();
    page.querySelector<HTMLFormElement>('form.editor')?.requestSubmit();
    await settle();
    expect(store.updates.length).toBe(1);
    expect(store.updates[0]?.nameEn).toBe('Levant Hotels & Resorts');
    expect(store.updates[0]?.brand?.franchisorName).toBe('Levant Hospitality Holdings');
    expect(store.updates[0]?.precedence).toBe(PRECEDENCE);
    expect(store.updates[0]?.brand?.royaltyPercent).toBe(ROYALTY);
    expect(store.reads).toBe(SECOND_READ);
    expect(texts('.c-tabs [role="tab"]').length).toBe(THREE + 1);
  });
});
