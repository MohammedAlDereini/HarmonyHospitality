import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { GroupsStore, ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import { GroupFilters, PropertyGroup } from '@harmony/platform-admin/domain';
import { AccessStore } from '@harmony/platform/access';
import { PageActions } from '@harmony/ui/shell';
import { GroupsPage } from './groups-page';

const PRECEDENCE = 10;
const HOTELS = 3;

const ritzCarlton: PropertyGroup = {
  id: 'g1',
  tenantId: 't1',
  typeCode: 'Brand',
  code: 'RITZ-CARLTON',
  externalIdentifier: 'RC-01',
  nameEn: 'The Ritz-Carlton',
  nameAr: 'ريتز كارلتون',
  description: null,
  precedence: PRECEDENCE,
  displayOrder: 1,
  isActive: true,
  currentMembers: HOTELS,
  brand: null,
  members: [],
};

class FakeStore {
  readonly filters = signal<GroupFilters>({ tenantId: null, typeCode: null, search: '', page: 1 });
  readonly rows = signal<readonly PropertyGroup[]>([ritzCarlton]);
  readonly total = signal(1);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  tenants: (string | null)[] = [];
  types: (string | null)[] = [];
  searched: string[] = [];
  ofTenant(id: string | null): void {
    this.tenants.push(id);
  }
  ofType(code: string | null): void {
    this.types.push(code);
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

const TYPE_CELL = 2;

describe('GroupsPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<GroupsPage>;
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
        { provide: GroupsStore, useValue: store },
        { provide: TenantsStore, useValue: { rows: signal([]) } },
        {
          provide: ReferenceStore,
          useValue: {
            choices: () => ({
              rows: signal([{ code: 'Brand', nameEn: 'Brand' }]),
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

  it('lists every group when no tenant is named; the row shows code, name, type, hotels, precedence, state', async () => {
    await settle();
    expect(store.tenants).toEqual([null]);
    expect(text('thead th')).toEqual(['Code', 'Name', 'Type', 'Hotels', 'Precedence', 'State']);
    expect(text('tr.grow td.key')).toEqual(['RITZ-CARLTON']);
    expect(text('tr.grow a.nm-link')).toEqual(['The Ritz-Carlton']);
    expect(text('tr.grow .sub')).toEqual(['RC-01']);
    expect(text('tr.grow td').slice(TYPE_CELL)).toEqual(['Brand', '3', '10', 'Active']);
    expect(page.querySelector('tr.grow .pill.on')).not.toBeNull();
    expect(page.querySelector('.grow a.nm-link')?.getAttribute('href')).toBe('/g1');
    fixture.componentRef.setInput('tenant', 't1');
    await settle();
    expect(store.tenants).toEqual([null, 't1']);
  });

  it('the search and the type filter go to the store; Add group opens the editor in the drawer', async () => {
    await settle();
    const input = page.querySelector<HTMLInputElement>('#groups-search');
    if (input === null) throw new Error('no search field');
    input.value = 'ritz';
    page.querySelector('form')?.requestSubmit();
    expect(store.searched).toEqual(['ritz']);
    const typeBox = page.querySelector<HTMLSelectElement>('#groups-type');
    if (typeBox === null) throw new Error('no type filter');
    typeBox.value = 'Brand';
    typeBox.dispatchEvent(new Event('change'));
    expect(store.types).toEqual(['Brand']);
    const offered = TestBed.inject(PageActions).template();
    if (offered === null) throw new Error('nothing offered to the slot');
    const slot = document.createElement('div');
    const view = offered.createEmbeddedView(undefined);
    view.detectChanges();
    view.rootNodes.forEach((node: Node) => slot.appendChild(node));
    slot.querySelector<HTMLButtonElement>('.pbtn')?.click();
    await settle();
    expect(page.querySelector('dialog.drawer')?.hasAttribute('open')).toBe(true);
    expect(page.querySelector('.drawer-h h2')?.textContent).toBe('New group');
    expect(page.querySelector('dialog.drawer hr-group-editor')).not.toBeNull();
    view.destroy();
  });
});
