import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ReferenceStore } from '@harmony/platform-admin/data-access';
import { ReferenceRow, ReferenceTable } from '@harmony/platform-admin/domain';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { RowEditor } from './row-editor';
import { tableByKey } from './tables';

const OK = 200;
const BAD_REQUEST = 400;
const MINOR_UNITS = 3;

const must = (table: ReferenceTable | null): ReferenceTable => {
  if (table === null) throw new Error('table missing');
  return table;
};
const currencies = must(tableByKey('currencies'));
const countries = must(tableByKey('countries'));
const dinar: ReferenceRow = {
  code: 'JOD',
  nameEn: 'Jordanian dinar',
  nameAr: 'دينار أردني',
  minorUnits: MINOR_UNITS,
  isActive: true,
};

@Component({
  selector: 'hr-host',
  imports: [RowEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-row-editor [table]="table()" [row]="row()" (done)="closed.set(closed() + 1)" />`,
})
class Host {
  readonly table = signal<ReferenceTable>(currencies);
  readonly row = signal<ReferenceRow | null>(null);
  readonly closed = signal(0);
}

describe('hr-row-editor', () => {
  const created: { controller: string; body: Readonly<Record<string, unknown>>; key: IntentKey }[] =
    [];
  const updated: { controller: string; body: Readonly<Record<string, unknown>> }[] = [];
  let answer: ApiResult<unknown>;

  const render = async (table: ReferenceTable, row: ReferenceRow | null = null) => {
    created.length = 0;
    updated.length = 0;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ReferenceStore,
          useValue: {
            table: () => ({
              rows: signal([dinar]),
              loading: signal(false),
              problem: signal(null),
              scope: signal({}),
              load: () => undefined,
            }),
            create: (
              controller: string,
              body: Readonly<Record<string, unknown>>,
              key: IntentKey,
            ) => {
              created.push({ controller, body, key });
              return Promise.resolve(answer);
            },
            update: (controller: string, body: Readonly<Record<string, unknown>>) => {
              updated.push({ controller, body });
              return Promise.resolve(answer);
            },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.table.set(table);
    fixture.componentInstance.row.set(row);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async (): Promise<void> => {
      await new Promise((resolve) => setTimeout(resolve, 1));
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const type = async (key: string, value: string): Promise<void> => {
      const box = page.querySelector<HTMLInputElement | HTMLSelectElement>(`[data-field="${key}"]`);
      if (box === null) throw new Error(`no ${key}`);
      box.value = value;
      box.dispatchEvent(new Event('input'));
      box.dispatchEvent(new Event('change'));
      await settle();
    };
    const send = async (): Promise<void> => {
      page.querySelector('form')?.requestSubmit();
      await settle();
    };
    return { fixture, page, type, send, settle };
  };

  it('a new currency: the code and both names are required; a three-letter code is sent upper as typed, with the key', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render(currencies);
    expect(editor.page.querySelector('[data-field="isActive"]')).toBeNull();
    await editor.send();
    expect(created).toEqual([]);
    expect(editor.page.querySelector('#row-code-error')?.textContent).toContain('code');
    await editor.type('code', ' usd ');
    await editor.type('nameEn', 'US dollar');
    await editor.type('nameAr', 'دولار أمريكي');
    await editor.send();
    expect(created).toHaveLength(1);
    expect(created[0]?.controller).toBe('Currency');
    expect(created[0]?.body).toEqual({ code: 'usd', nameEn: 'US dollar', nameAr: 'دولار أمريكي' });
    expect(created[0]?.key.value).not.toBe('');
    expect(editor.fixture.componentInstance.closed()).toBe(1);
  });

  it('editing a currency: the code is read-only, the active flag appears, Save sends the whole row with its key', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render(currencies, dinar);
    const code = editor.page.querySelector<HTMLInputElement>('[data-field="code"]');
    expect(code?.value).toBe('JOD');
    expect(code?.readOnly).toBe(true);
    expect(editor.page.querySelector<HTMLInputElement>('[data-field="isActive"]')?.checked).toBe(
      true,
    );
    await editor.type('nameEn', 'Jordanian dinar (JOD)');
    await editor.send();
    expect(updated).toHaveLength(1);
    expect(updated[0]?.body).toEqual({
      code: 'JOD',
      nameEn: 'Jordanian dinar (JOD)',
      nameAr: 'دينار أردني',
      isActive: true,
    });
  });

  it('a select takes its choices from another table; a server field code lands under its field', async () => {
    answer = { kind: 'invalid', status: BAD_REQUEST, codes: ['10415'] };
    const editor = await render(countries);
    const options = [...editor.page.querySelectorAll('[data-field="defaultCurrency"] option')].map(
      (option) => option.textContent,
    );
    expect(options).toEqual(['Choose', 'Jordanian dinar']);
    await editor.type('code', 'jo');
    await editor.type('alpha3', 'jor');
    await editor.type('nameEn', 'Jordan');
    await editor.type('nameAr', 'الأردن');
    await editor.type('dialCode', '+962');
    await editor.type('defaultCurrency', 'JOD');
    await editor.send();
    expect(created).toHaveLength(1);
    expect(editor.page.querySelector('#row-dialCode-error')?.textContent).toContain('plus sign');
    expect(editor.fixture.componentInstance.closed()).toBe(0);
  });
});
