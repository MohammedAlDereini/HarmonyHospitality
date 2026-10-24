import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReferenceStore, TableRows } from '@harmony/platform-admin/data-access';
import { ReferenceRow } from '@harmony/platform-admin/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { PageActions } from '@harmony/ui/shell';
import { ReferenceTablePage } from './reference-table-page';

const OK = 200;
const MINOR_UNITS = 3;

const dinar: ReferenceRow = {
  code: 'JOD',
  nameEn: 'Jordanian dinar',
  nameAr: 'دينار أردني',
  minorUnits: MINOR_UNITS,
  isActive: true,
};
const night: ReferenceRow = { id: 'c1', name: 'Shifts', description: null, isGlobal: true };

class FakeStore {
  readonly tables = new Map<string, TableRows & { loads: Record<string, unknown>[] }>();
  removed: { controller: string; id: string; key: IntentKey }[] = [];
  table(controller: string): TableRows {
    const known = this.tables.get(controller);
    if (known !== undefined) return known;
    const loads: Record<string, unknown>[] = [];
    const made = {
      rows: signal<readonly ReferenceRow[]>(controller === 'Currency' ? [dinar] : [night]),
      loading: signal(false),
      problem: signal<string | null>(null),
      scope: signal<Readonly<Record<string, unknown>>>({}),
      loads,
      load: (scope?: Readonly<Record<string, unknown>>) => {
        loads.push(scope ?? {});
      },
    };
    this.tables.set(controller, made);
    return made;
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
    [...page.querySelectorAll(selector)].map((cell) => cell.textContent.trim());
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

  it('reads the table on arrival and shows its columns; Yes and No for the flags; Add row and Edit', async () => {
    await open('currencies');
    expect(store.tables.get('Currency')?.loads).toEqual([{}]);
    expect(text('.xrow .obj b')).toEqual(['JOD', 'Jordanian dinar', 'دينار أردني', '3', 'Yes']);
    expect(text('.xrow .obj span')).toEqual([
      'Code',
      'Name (English)',
      'Name (Arabic)',
      'minorUnits',
      'Active',
    ]);
    expect(page.querySelector('[data-act="delete"]')).toBeNull();
    page.querySelector<HTMLButtonElement>('[data-act="edit"]')?.click();
    await settle();
    expect(page.querySelector('hr-row-editor')).not.toBeNull();
    const offered = TestBed.inject(PageActions).template();
    expect(offered).not.toBeNull();
  });

  it('lookup values wait for a category, then read that category; lookups may be deleted after a confirmation', async () => {
    await open('lookup-values');
    expect(store.tables.get('LookupValue')?.loads).toEqual([]);
    expect(page.textContent).toContain('Choose a category first');
    const chooser = page.querySelector<HTMLSelectElement>('[data-scope]');
    if (chooser === null) throw new Error('no category chooser');
    chooser.value = 'c1';
    chooser.dispatchEvent(new Event('change'));
    await settle();
    expect(store.tables.get('LookupValue')?.loads).toEqual([{ lookupCategoryId: 'c1' }]);
    page.querySelector<HTMLButtonElement>('[data-act="delete"]')?.click();
    await settle();
    expect(page.querySelector('.confirm .warning')?.textContent).toContain('cannot be undone');
    page.querySelector<HTMLButtonElement>('[data-act="delete-confirm"]')?.click();
    await settle();
    expect(store.removed.map((call) => `${call.controller}:${call.id}`)).toEqual([
      'LookupValue:c1',
    ]);
    expect(store.removed[0]?.key.value).not.toBe('');
  });

  it('an unknown table says so', async () => {
    await open('no-such-table');
    expect(page.textContent).toContain('no such table');
  });
});
