import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SettingsStore } from '@harmony/administration/data-access';
import { EffectiveSetting, SettingPlace } from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { SettingsPage } from './settings-page';

const OK = 200;
const ACCEPTED = 202;
const UUID_LENGTH = 36;
const LEVANT_PRECEDENCE = 10;
const TRACE_STEPS = 4;
// Runs of whitespace collapse to one space: matches "a \n b" (→ "a b"); rejects "ab".
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const checkIn: EffectiveSetting = {
  key: 'frontoffice.checkInTime',
  ownerService: 'FrontOffice',
  dataType: 'time',
  outcome: 'Value',
  value: '13:00',
  source: 'PropertyGroup',
  sourceCode: 'LEVANT',
  trace: [
    {
      level: 'Property',
      id: 'p-amman',
      code: 'AMMAN',
      precedence: null,
      value: null,
      verdict: 'NotSet',
    },
    {
      level: 'PropertyGroup',
      id: 'g-levant',
      code: 'LEVANT',
      precedence: LEVANT_PRECEDENCE,
      value: '13:00',
      verdict: 'Won',
    },
    {
      level: 'Tenant',
      id: null,
      code: null,
      precedence: null,
      value: '14:00',
      verdict: 'Overridden',
    },
    {
      level: 'SystemDefault',
      id: null,
      code: null,
      precedence: null,
      value: '15:00',
      verdict: 'Overridden',
    },
  ],
};
const lateFee: EffectiveSetting = {
  key: 'frontoffice.lateFee',
  ownerService: 'FrontOffice',
  dataType: 'decimal',
  outcome: 'Conflict',
  value: null,
  source: 'PropertyGroup',
  sourceCode: null,
  trace: [],
};
const resortOnly: EffectiveSetting = {
  ...lateFee,
  key: 'resort.beachTowels',
  outcome: 'NotApplicable',
  source: null,
};

class FakeStore {
  readonly settings = signal<readonly EffectiveSetting[]>([checkIn, lateFee, resortOnly]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  readonly calls: string[] = [];
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  reload(): void {
    this.calls.push('reload');
  }
  set(
    key: string,
    place: SettingPlace,
    value: string,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    this.calls.push(
      `set:${key}:${place.scope}:${place.scopeId ?? '-'}:${value}:${String(intent.value.length)}`,
    );
    return Promise.resolve(this.answer);
  }
  clear(key: string, place: SettingPlace): Promise<ApiResult<unknown>> {
    this.calls.push(`clear:${key}:${place.scope}:${place.scopeId ?? '-'}`);
    return Promise.resolve(this.answer);
  }
}

describe('SettingsPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<SettingsPage>;
  let page: HTMLElement;
  let mayManage = true;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const text = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );
  const click = (selector: string): void => {
    page.querySelector<HTMLElement>(selector)?.click();
  };
  const choose = async (selector: string, value: string): Promise<void> => {
    const box = page.querySelector<HTMLInputElement | HTMLSelectElement>(selector);
    if (box === null) throw new Error(`no ${selector}`);
    box.value = value;
    box.dispatchEvent(new Event('input'));
    box.dispatchEvent(new Event('change'));
    await settle();
  };
  const render = async (): Promise<void> => {
    TestBed.configureTestingModule({
      providers: [{ provide: AccessStore, useValue: { can: () => mayManage } }],
    });
    // The page provides its own SettingsStore; the fake takes that slot.
    TestBed.overrideComponent(SettingsPage, {
      set: { providers: [{ provide: SettingsStore, useValue: store }] },
    });
    fixture = TestBed.createComponent(SettingsPage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  };

  beforeEach(async () => {
    store = new FakeStore();
    mayManage = true;
    await render();
  });

  it('lists every setting with its value here and where it comes from; no value is worded, a conflict warns', () => {
    expect(text('tr.setting-row td.key')).toEqual([
      'frontoffice.checkInTime',
      'frontoffice.lateFee',
      'resort.beachTowels',
    ]);
    expect(text('tr.setting-row td.value')).toEqual([
      '13:00',
      'Two groups disagree',
      'Not for this kind of property',
    ]);
    expect(text('tr.setting-row td.from')).toEqual(['Group LEVANT', 'Group', '']);
    expect(page.querySelector('tr[data-outcome="Conflict"] .pill.warn')).not.toBeNull();
  });

  it('the drawer shows the trace in order; Save sets the value at the chosen place with a fresh key, then closes', async () => {
    click('tr[data-key="frontoffice.checkInTime"] [data-act="open-setting"]');
    await settle();
    expect(text('.tstep .where')).toEqual([
      'This hotel (AMMAN)',
      'Group LEVANT, precedence 10',
      'Whole customer',
      'Default',
    ]);
    expect(text('.tstep .pill')).toEqual(['Not set', 'Wins', 'Overridden', 'Overridden']);
    expect(text('#setting-place option')).toEqual([
      'This hotel (AMMAN)',
      'Group LEVANT',
      'Whole customer',
    ]);
    // Nothing set at the hotel: there is nothing to clear there.
    expect(page.querySelector<HTMLButtonElement>('[data-act="clear-setting"]')?.disabled).toBe(
      true,
    );
    await choose('#setting-value', '16:30');
    click('[data-act="save-setting"]');
    await settle();
    expect(store.calls).toEqual([
      `set:frontoffice.checkInTime:Property:p-amman:16:30:${String(UUID_LENGTH)}`,
    ]);
  });

  it('Clear at a place that holds a value; a refusal is worded in the drawer and nothing closes', async () => {
    click('tr[data-key="frontoffice.checkInTime"] [data-act="open-setting"]');
    await settle();
    await choose('#setting-place', '2');
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00116'] };
    click('[data-act="clear-setting"]');
    await settle();
    expect(store.calls).toEqual(['clear:frontoffice.checkInTime:Tenant:-']);
    expect(text('hr-setting-detail [role="alert"]').join(' ')).toContain('whole customer');
  });

  it('without ManageSettings the drawer only explains: no form', async () => {
    mayManage = false;
    fixture.destroy();
    TestBed.resetTestingModule();
    await render();
    click('tr[data-key="frontoffice.checkInTime"] [data-act="open-setting"]');
    await settle();
    expect(text('.tstep .where')).toHaveLength(TRACE_STEPS);
    expect(page.querySelector('hr-setting-detail form')).toBeNull();
  });
});
