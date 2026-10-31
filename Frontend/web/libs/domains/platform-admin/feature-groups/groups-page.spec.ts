import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { GroupsStore, ReferenceStore } from '@harmony/platform-admin/data-access';
import { GroupFilters, PropertyGroup } from '@harmony/platform-admin/domain';
import { TenantChoicesStore } from '@harmony/platform-operations/data-access';
import { AccessStore } from '@harmony/platform/access';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { GroupsPage } from './groups-page';

const PRECEDENCE = 100;
const REGION_PRECEDENCE = 50;
const HOTELS = 3;
const TWO = 2;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const levant: PropertyGroup = {
  id: 'g1',
  tenantId: 't1',
  typeCode: 'Brand',
  code: 'LEVANT-BRAND',
  externalIdentifier: 'LEV-BR-01',
  nameEn: 'Levant Hotels',
  nameAr: 'فنادق المشرق',
  description: null,
  precedence: PRECEDENCE,
  displayOrder: 1,
  isActive: true,
  currentMembers: HOTELS,
  brand: {
    themeReference: 'levant-classic',
    logoFileId: null,
    websiteUrl: 'levanthotels.example',
    franchisorName: 'Levant Hospitality Holdings',
    agreementStartsOn: '2024-01-01',
    agreementEndsOn: '2028-12-31',
    royaltyPercent: null,
  },
  members: [],
};
const north: PropertyGroup = {
  ...levant,
  id: 'g2',
  typeCode: 'Region',
  code: 'LEVANT-NORTH',
  externalIdentifier: null,
  nameEn: 'Northern region',
  nameAr: 'المنطقة الشمالية',
  description: 'Amman and the Dead Sea',
  precedence: REGION_PRECEDENCE,
  isActive: false,
  currentMembers: TWO,
  brand: null,
};

class FakeStore {
  readonly filters = signal<GroupFilters>({
    tenantId: null,
    typeCode: null,
    isActive: null,
    search: '',
    page: 1,
  });
  readonly rows = signal<readonly PropertyGroup[]>([levant, north]);
  readonly total = signal(TWO);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  tenants: (string | null)[] = [];
  types: (string | null)[] = [];
  states: (boolean | null)[] = [];
  searched: string[] = [];
  ofTenant(id: string | null): void {
    this.tenants.push(id);
  }
  ofType(code: string | null): void {
    this.types.push(code);
  }
  showState(isActive: boolean | null): void {
    this.states.push(isActive);
  }
  search(text: string): void {
    this.searched.push(text);
  }
  goTo(): void {
    return undefined;
  }
  reload(): void {
    return undefined;
  }
}

describe('GroupsPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<GroupsPage>;
  let page: HTMLElement;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const text = (selector: string): string =>
    page.querySelector(selector)?.textContent.replace(whitespace, ' ').trim() ?? '';
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );
  /** Opens a facet's popover and ticks the option with this value. */
  const pick = async (label: string, value: string): Promise<void> => {
    const facet = [...page.querySelectorAll('hr-facet')].find((f) =>
      f.querySelector('.chip-btn')?.textContent.startsWith(label),
    );
    if (facet === undefined) throw new Error(`no facet ${label}`);
    const option = facet.querySelector<HTMLInputElement>(`input[value="${value}"]`);
    if (option === null) throw new Error(`no option ${value}`);
    option.click();
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
            choices: () => ({
              rows: signal([
                { code: 'Brand', nameEn: 'Brand', nameAr: 'علامة' },
                { code: 'Region', nameEn: 'Region', nameAr: 'منطقة' },
                { code: 'Owner', nameEn: 'Owner', nameAr: 'مالك' },
              ]),
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
    fixture = TestBed.createComponent(GroupsPage);
    page = fixture.nativeElement as HTMLElement;
  });

  it('with no groups the lead and the card say none yet; narrowed by a facet they say none match', async () => {
    store.rows.set([]);
    store.total.set(0);
    await settle();
    expect(text('.lead')).toBe('No groups yet');
    expect(text('.c-empty b')).toBe('No groups yet');
    expect(text('.c-empty p')).toBe('New group adds the first.');
    store.filters.update((filters) => ({ ...filters, typeCode: 'Brand' }));
    await settle();
    expect(text('.lead')).toBe('No group matches');
    expect(text('.c-empty b')).toBe('No groups match');
  });

  it('counts the groups by type in the lead; each row says the group, its tenant, type, hotels, precedence, agreement and state', async () => {
    await settle();
    expect(store.tenants).toEqual([null]);
    expect(text('.lead')).toBe('2 groups · 1 Brand · 1 Region · 1 inactive');
    expect(texts('thead th')).toEqual([
      'Group',
      'Tenant',
      'Type',
      'Hotels',
      'Precedence',
      'Agreement',
      'State',
      'Actions',
    ]);
    expect(texts('tr.grow a.nm-link')).toEqual([
      'LEVANT-BRAND · Levant Hotels',
      'LEVANT-NORTH · Northern region',
    ]);
    expect(texts('tr.grow .sub')).toEqual([
      'LEV-BR-01 · franchisor Levant Hospitality Holdings',
      'Amman and the Dead Sea',
    ]);
    const first = [...(page.querySelector('tr.grow')?.querySelectorAll('td') ?? [])].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );
    expect(first.slice(1, first.length - 1)).toEqual([
      'LEV',
      'Brand',
      String(HOTELS),
      String(PRECEDENCE),
      '01 Jan 2024 – 31 Dec 2028',
      'Active',
    ]);
    expect(page.querySelector('tr.grow .chip[data-s="vip"]')?.textContent.trim()).toBe('Brand');
    expect(texts('tr.grow td:nth-child(7) .chip')).toEqual(['Active', 'Inactive']);
    expect(texts('tr.grow td:nth-child(6)')).toEqual(['01 Jan 2024 – 31 Dec 2028', '—']);
    expect(page.querySelector('.grow a.nm-link')?.getAttribute('href')).toBe('/g1');
    expect(text('.c-filters .count')).toBe('2 of 2 · by code');
    fixture.componentRef.setInput('tenant', 't1');
    await settle();
    expect(store.tenants).toEqual([null, 't1']);
  });

  it('the search and the three facets go to the store', async () => {
    await settle();
    const input = page.querySelector<HTMLInputElement>('#groups-search');
    if (input === null) throw new Error('no search field');
    input.value = 'levant';
    page.querySelector<HTMLFormElement>('form.c-filters')?.requestSubmit();
    expect(store.searched).toEqual(['levant']);
    await pick('Tenant', 't1');
    expect(store.tenants).toEqual([null, 't1']);
    await pick('Type', 'Region');
    expect(store.types).toEqual(['Region']);
    await pick('State', 'inactive');
    expect(store.states).toEqual([false]);
  });

  it('New group opens the editor in the drawer', async () => {
    await settle();
    page.querySelector<HTMLButtonElement>('.ph-acts .btn.solid')?.click();
    await settle();
    expect(page.querySelector('dialog.drawer')?.hasAttribute('open')).toBe(true);
    expect(page.querySelector('.drawer-h h2')?.textContent).toBe('New group');
    expect(page.querySelector('dialog.drawer hr-group-editor')).not.toBeNull();
  });
});
