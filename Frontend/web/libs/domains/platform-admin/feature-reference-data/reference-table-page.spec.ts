import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  OptionRows,
  ReferenceFilters,
  ReferenceQuery,
  ReferenceStore,
  TableRows,
} from '@harmony/platform-admin/data-access';
import { ReferenceRow } from '@harmony/platform-admin/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { DESCENDING, SortOrder } from '@harmony/shared/domain';
import { Toasts } from '@harmony/ui/overlays';
import { PageActions } from '@harmony/ui/shell';
import { ReferenceTablePage } from './reference-table-page';

const OK = 200;
const MINOR_UNITS = 3;
// The sort arrows a header carries. Matches "▲"; rejects "Code".
// Tester: https://regex101.com/?regex=%5B%E2%96%B2%E2%96%BC%5D&testString=Code%E2%96%B2
const arrows = /[▲▼]/g;

const dinar: ReferenceRow = {
  code: 'JOD',
  nameEn: 'Jordanian dinar',
  nameAr: 'دينار أردني',
  minorUnits: MINOR_UNITS,
  isActive: true,
};
const night: ReferenceRow = {
  id: 'v1',
  name: 'Night',
  description: null,
  isActive: false,
  isGlobal: true,
};
const shifts: ReferenceRow = { id: 'c1', name: 'Shifts', description: null, isGlobal: true };

interface FakeTable extends TableRows {
  readonly loads: Record<string, unknown>[];
  readonly queries: ReferenceQuery[];
}

class FakeStore {
  readonly tables = new Map<string, FakeTable>();
  removed: { controller: string; id: string; key: IntentKey }[] = [];
  table(controller: string, defaultSort: readonly SortOrder[] = []): TableRows {
    const known = this.tables.get(controller);
    if (known !== undefined) return known;
    const loads: Record<string, unknown>[] = [];
    const queries: ReferenceQuery[] = [];
    const sort = signal<SortOrder | null>(defaultSort[0] ?? null);
    const filters = signal<ReferenceFilters>({});
    const page = signal(1);
    const made: FakeTable = {
      rows: signal<readonly ReferenceRow[]>(controller === 'Currency' ? [dinar] : [night]),
      total: signal(1),
      loading: signal(false),
      problem: signal<string | null>(null),
      page,
      sort,
      filters,
      scope: signal<Readonly<Record<string, unknown>>>({}),
      loads,
      queries,
      load: (scope?: Readonly<Record<string, unknown>>) => {
        loads.push(scope ?? {});
      },
      query: (change: ReferenceQuery) => {
        queries.push(change);
        if (change.sort !== undefined) sort.set(change.sort);
        if (change.filters !== undefined) filters.set(change.filters);
        if (change.page !== undefined) page.set(change.page);
      },
    };
    this.tables.set(controller, made);
    return made;
  }
  choices(controller: string): OptionRows {
    return {
      rows: signal<readonly ReferenceRow[]>(controller === 'LookupCategory' ? [shifts] : []),
      loading: signal(false),
      load: () => undefined,
    };
  }
  remove(controller: string, id: string, key: IntentKey): Promise<ApiResult<unknown>> {
    this.removed.push({ controller, id, key });
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
}

describe('ReferenceTablePage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<ReferenceTablePage>;
  let page: HTMLElement;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const text = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) => cell.textContent.replace(arrows, '').trim());
  const open = async (table: string): Promise<void> => {
    fixture = TestBed.createComponent(ReferenceTablePage);
    fixture.componentRef.setInput('table', table);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  };

  beforeEach(() => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        { provide: ReferenceStore, useValue: store },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
  });

  it('reads the table on arrival as a real table: headers in words, the key bold, numbers right, state as a pill; sorting flips the column; the lead says what the table is for', async () => {
    await open('currencies');
    expect(store.tables.get('Currency')?.loads).toEqual([{}]);
    expect(page.querySelector('.page-head .lead')?.textContent).toContain('currencies a hotel can');
    expect(text('thead th')).toEqual([
      'Code',
      'English name',
      'Arabic name',
      'Decimals',
      'State',
      'Actions',
    ]);
    expect(text('tbody tr.xrow td')).toEqual([
      'JOD',
      'Jordanian dinar',
      'دينار أردني',
      '3',
      'Active',
      'Edit',
    ]);
    expect(page.querySelector('tbody td.key')?.textContent.trim()).toBe('JOD');
    expect(page.querySelector('tbody td.num')?.textContent.trim()).toBe('3');
    expect(page.querySelector('tbody .pill.on')?.textContent.trim()).toBe('Active');
    expect(page.querySelector('thead th')?.getAttribute('aria-sort')).toBe('ascending');
    page.querySelector<HTMLButtonElement>('thead th .sort')?.click();
    await settle();
    expect(store.tables.get('Currency')?.queries).toEqual([
      { sort: { memberName: 'Code', sortOrder: DESCENDING } },
    ]);
    expect(page.querySelector('[data-act="delete"]')).toBeNull();
    expect(TestBed.inject(PageActions).template()).not.toBeNull();
  });

  it('the toolbar filters: Apply sends what was typed, Clear empties it', async () => {
    await open('currencies');
    const search = page.querySelector<HTMLInputElement>('[data-filter="searchText"]');
    const state = page.querySelector<HTMLSelectElement>('[data-filter="isActive"]');
    if (search === null || state === null) throw new Error('no filters');
    search.value = 'din';
    search.dispatchEvent(new Event('input'));
    state.value = 'true';
    state.dispatchEvent(new Event('change'));
    page.querySelector<HTMLFormElement>('form.toolbar')?.requestSubmit();
    await settle();
    expect(store.tables.get('Currency')?.queries).toEqual([
      { filters: { searchText: 'din', isActive: 'true' } },
    ]);
    page.querySelector<HTMLButtonElement>('.tacts .quiet')?.click();
    await settle();
    expect(store.tables.get('Currency')?.queries.at(-1)).toEqual({ filters: {} });
  });

  it('Edit opens the drawer with the row; the context bar action opens it for a new row', async () => {
    await open('currencies');
    const drawer = page.querySelector<HTMLDialogElement>('dialog.drawer');
    if (drawer === null) throw new Error('no drawer');
    expect(drawer.hasAttribute('open')).toBe(false);
    page.querySelector<HTMLButtonElement>('[data-act="edit"]')?.click();
    await settle();
    expect(drawer.hasAttribute('open')).toBe(true);
    expect(page.querySelector('.drawer-h h2')?.textContent).toBe('Edit currency');
    expect(page.querySelector('.drawer-h .lead')?.textContent).toContain('cannot change');
    expect(page.querySelector('hr-row-editor [data-field="code"]')).not.toBeNull();
    page.querySelector<HTMLButtonElement>('.drawer-x')?.click();
    await settle();
    expect(drawer.hasAttribute('open')).toBe(false);
    expect(page.querySelector('hr-row-editor')).toBeNull();
  });

  it('lookup values wait for a category, then read that category; a lookup row is deleted after the confirm dialog, with a toast', async () => {
    await open('lookup-values');
    expect(store.tables.get('LookupValue')?.loads).toEqual([]);
    expect(page.textContent).toContain('Choose a category first');
    const chooser = page.querySelector<HTMLSelectElement>('[data-scope]');
    if (chooser === null) throw new Error('no category chooser');
    expect([...chooser.options].map((option) => option.textContent.trim())).toEqual([
      'Choose a category',
      'Shifts',
    ]);
    chooser.value = 'c1';
    chooser.dispatchEvent(new Event('change'));
    await settle();
    expect(store.tables.get('LookupValue')?.loads).toEqual([{ lookupCategoryId: 'c1' }]);
    expect(page.querySelector('tbody .pill.off')?.textContent.trim()).toBe('Inactive');
    page.querySelector<HTMLButtonElement>('[data-act="delete"]')?.click();
    await settle();
    const confirm = page.querySelector<HTMLDialogElement>('dialog.dlg');
    expect(confirm?.hasAttribute('open')).toBe(true);
    expect(page.querySelector('.dlg-h')?.textContent).toBe('Delete this lookup value?');
    page.querySelector<HTMLButtonElement>('.dlg-a .solid')?.click();
    await settle();
    expect(store.removed.map((call) => `${call.controller}:${call.id}`)).toEqual([
      'LookupValue:v1',
    ]);
    expect(store.removed[0]?.key.value).not.toBe('');
    expect(TestBed.inject(Toasts).list()[0]?.text).toBe('Lookup value deleted.');
  });

  it('an unknown table says so', async () => {
    await open('no-such-table');
    expect(page.textContent).toContain('no such table');
  });
});
