import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PropertiesStore, ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import { Property, PropertyFilters } from '@harmony/platform-admin/domain';
import { AccessStore } from '@harmony/platform/access';
import { PageActions } from '@harmony/ui/shell';
import { PropertiesPage } from './properties-page';

const ROOMS = 120;

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
  licensedRooms: null,
  roomsAboveLicence: false,
  subscriptions: [],
  contacts: [],
};

class FakeStore {
  readonly filters = signal<PropertyFilters>({ tenantId: null, search: '', page: 1 });
  readonly rows = signal<readonly Property[]>([amman]);
  readonly total = signal(1);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  tenants: (string | null)[] = [];
  searched: string[] = [];
  ofTenant(id: string | null): void {
    this.tenants.push(id);
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

const ROOMS_CELL = 2;

describe('PropertiesPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<PropertiesPage>;
  let page: HTMLElement;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const text = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) => cell.textContent.trim());

  beforeEach(() => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PropertiesStore, useValue: store },
        { provide: TenantsStore, useValue: { rows: signal([]) } },
        {
          provide: ReferenceStore,
          useValue: {
            choices: () => ({
              rows: signal([]),
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
  });

  it('lists every property when no tenant is named; a ?tenant= narrows the list', async () => {
    await settle();
    expect(store.tenants).toEqual([null]);
    expect(text('thead th')).toEqual(['Code', 'Name', 'Rooms', 'Currency · language', 'State']);
    expect(text('tr.prow td.key')).toEqual(['AMM']);
    expect(text('tr.prow a.nm-link')).toEqual(['Ritz Amman']);
    expect(text('tr.prow .sub')).toEqual(['JO · Asia/Amman']);
    expect(text('tr.prow td').slice(ROOMS_CELL)).toEqual(['120', 'JOD · en', 'Active']);
    expect(page.querySelector('tr.prow .pill.on')).not.toBeNull();
    expect(page.querySelector('.prow a.nm-link')?.getAttribute('href')).toBe('/p1');
    fixture.componentRef.setInput('tenant', 't1');
    await settle();
    expect(store.tenants).toEqual([null, 't1']);
  });

  it('the search goes to the store; Add property opens the editor with the tenant known', async () => {
    fixture.componentRef.setInput('tenant', 't1');
    await settle();
    const input = page.querySelector<HTMLInputElement>('#properties-search');
    if (input === null) throw new Error('no search field');
    input.value = 'amm';
    page.querySelector('form')?.requestSubmit();
    expect(store.searched).toEqual(['amm']);
    const offered = TestBed.inject(PageActions).template();
    if (offered === null) throw new Error('nothing offered to the slot');
    const slot = document.createElement('div');
    const view = offered.createEmbeddedView(undefined);
    view.detectChanges();
    view.rootNodes.forEach((node: Node) => slot.appendChild(node));
    slot.querySelector<HTMLButtonElement>('.pbtn')?.click();
    await settle();
    expect(page.querySelector('dialog.drawer')?.hasAttribute('open')).toBe(true);
    expect(page.querySelector('.drawer-h h2')?.textContent).toBe('New property');
    expect(page.querySelector('dialog.drawer hr-property-editor')).not.toBeNull();
    view.destroy();
  });
});
