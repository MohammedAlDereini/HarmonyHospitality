import { Signal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { OptionRows, ReferenceStore } from '@harmony/platform-admin/data-access';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult } from '@harmony/platform/http';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { SettingsConsolePage } from './settings-console-page';

type ReferenceRow = ReturnType<OptionRows['rows']>[number];

const OK = 200;
const THREE = 3;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const twoStep: ReferenceRow = {
  key: 'identity.twoStepForAdministrators',
  ownerService: 'Identity',
  dataType: 'bool',
  systemDefault: 'true',
  appliesToTypes: null,
  isActive: true,
};
const sessionHours: ReferenceRow = {
  key: 'identity.absoluteSessionHours',
  ownerService: 'Identity',
  dataType: 'int',
  systemDefault: '12',
  appliesToTypes: null,
  isActive: true,
};
const checkIn: ReferenceRow = {
  key: 'frontoffice.checkInTime',
  ownerService: 'FrontOffice',
  dataType: 'time',
  systemDefault: '15:00',
  appliesToTypes: ['HOTEL', 'RESORT'],
  isActive: true,
};

class FakeStore {
  readonly rows = signal<readonly ReferenceRow[]>([checkIn, twoStep, sessionHours]);
  updates: Record<string, unknown>[] = [];
  choices(): { rows: Signal<readonly ReferenceRow[]>; loading: Signal<boolean>; load: () => void } {
    return { rows: this.rows, loading: signal(false), load: () => undefined };
  }
  count(): { total: Signal<number> } {
    return { total: signal(THREE) };
  }
  update(
    _controller: string,
    body: Readonly<Record<string, unknown>>,
  ): Promise<ApiResult<unknown>> {
    this.updates.push({ ...body });
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
}

describe('SettingsConsolePage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<SettingsConsolePage>;
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

  beforeEach(async () => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: ReferenceStore, useValue: store },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    fixture = TestBed.createComponent(SettingsConsolePage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('groups the registered keys by the service that owns them and shows each default with the right control', () => {
    expect(text('.lead')).toBe(
      'Platform scope · 3 registered settings · resolution: property › group › tenant › platform default',
    );
    expect(texts('nav.groups button .t')).toEqual(['FrontOffice', 'Identity']);
    expect(texts('nav.groups button .n')).toEqual(['1', '2']);
    expect(text('section.settings h3')).toBe('FrontOffice');
    expect(texts('.row .text b')).toEqual(['frontoffice.checkInTime']);
    expect(texts('.row .sub')).toEqual(['time · HOTEL, RESORT']);
    expect(page.querySelector<HTMLInputElement>('.row input[type="time"]')?.value).toBe('15:00');
    expect(page.querySelector('[data-act="save-changes"]')?.hasAttribute('disabled')).toBe(true);
    expect(page.querySelector('a[href="/reference/settings"]')).not.toBeNull();
  });

  it('a changed default is counted and saved through the registry, one update per key', async () => {
    page.querySelectorAll<HTMLButtonElement>('nav.groups button')[1]?.click();
    await settle();
    expect(texts('.row .text b')).toEqual([
      'identity.twoStepForAdministrators',
      'identity.absoluteSessionHours',
    ]);
    const hours = page.querySelector<HTMLInputElement>('.row input[type="number"]');
    const twoStepBox = page.querySelector<HTMLInputElement>('.row .c-switch input');
    if (hours === null || twoStepBox === null) throw new Error('no controls');
    expect(twoStepBox.checked).toBe(true);
    hours.value = '8';
    hours.dispatchEvent(new Event('input'));
    twoStepBox.checked = false;
    twoStepBox.dispatchEvent(new Event('change'));
    await settle();
    expect(text('[data-changes] span')).toBe('2 defaults changed and not saved yet.');
    page.querySelector<HTMLButtonElement>('[data-act="save-changes"]')?.click();
    await settle();
    await settle();
    expect(store.updates).toEqual([
      {
        key: 'identity.twoStepForAdministrators',
        ownerService: 'Identity',
        systemDefault: 'false',
        appliesToTypes: [],
        isActive: true,
      },
      {
        key: 'identity.absoluteSessionHours',
        ownerService: 'Identity',
        systemDefault: '8',
        appliesToTypes: [],
        isActive: true,
      },
    ]);
    expect(page.querySelector('[data-changes]')).toBeNull();
  });
});
