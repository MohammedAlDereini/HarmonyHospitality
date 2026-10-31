import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { InvitationsStore, RolesStore, UsersStore } from '@harmony/administration/data-access';
import {
  AccountState,
  TwoStepFilter,
  UserAccount,
  UserFilters,
} from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult } from '@harmony/platform/http';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { PeoplePage } from './people-page';

const OK = 200;
const SEVEN = 7;
const FIVE = 5;
const TWO = 2;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const omar: UserAccount = {
  id: 'u1',
  userName: 'omar.haddad@harmony.example',
  email: 'omar.haddad@harmony.example',
  displayName: 'Omar Haddad',
  twoFactorEnabled: true,
  twoStepExemptReason: null,
  state: AccountState.active,
  stateReason: null,
  isDirectoryManaged: false,
  createdDate: '2025-09-01T08:00:00',
  lastSignInUtc: '2026-10-29T09:12:00Z',
  lastSignInDevice: 'Windows · Edge 130',
  roleIds: ['r1'],
};
const yousef: UserAccount = {
  ...omar,
  id: 'u2',
  userName: 'yousef.karam@harmony.example',
  email: 'yousef.karam@harmony.example',
  displayName: 'Yousef Karam',
  twoFactorEnabled: false,
  createdDate: '2026-10-12T08:00:00',
  lastSignInUtc: null,
  lastSignInDevice: null,
  roleIds: ['r2'],
};

class FakeStore {
  readonly filters = signal<UserFilters>({
    search: '',
    state: null,
    twoStep: null,
    roleId: null,
    page: 1,
  });
  readonly rows = signal<readonly UserAccount[]>([omar, yousef]);
  readonly total = signal(SEVEN);
  readonly census = signal({ total: SEVEN, active: FIVE, suspended: 1, terminated: 1 });
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  counted = 0;
  states: (AccountState | null)[] = [];
  twoSteps: (TwoStepFilter | null)[] = [];
  roles: (string | null)[] = [];
  searched: string[] = [];
  countAll(): void {
    this.counted += 1;
  }
  search(text: string): void {
    this.searched.push(text);
  }
  showState(state: AccountState | null): void {
    this.states.push(state);
  }
  showTwoStep(twoStep: TwoStepFilter | null): void {
    this.twoSteps.push(twoStep);
  }
  showRole(roleId: string | null): void {
    this.roles.push(roleId);
  }
  goTo(): void {
    return undefined;
  }
  reload(): void {
    return undefined;
  }
  reinstate(): Promise<ApiResult<unknown>> {
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
}

describe('PeoplePage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<PeoplePage>;
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

  beforeEach(async () => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: UsersStore, useValue: store },
        {
          provide: InvitationsStore,
          useValue: { rows: signal([{ id: 'i1' }, { id: 'i2' }]), reload: () => undefined },
        },
        {
          provide: RolesStore,
          useValue: {
            roles: signal([
              { id: 'r1', code: 'SUPERADMIN', nameEn: 'Super administrator', nameAr: null },
              { id: 'r2', code: 'SUPPORT', nameEn: 'Support', nameAr: null },
            ]),
            loading: signal(false),
            problem: signal(null),
            load: () => undefined,
          },
        },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    fixture = TestBed.createComponent(PeoplePage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('counts the platform’s accounts in the lead and the tabs; each row says the person, roles, two-step, state, last sign-in and created', () => {
    expect(store.counted).toBe(1);
    expect(text('.lead')).toBe(
      '7 platform accounts · 5 active · 1 suspended · 1 terminated · 2 invitations waiting',
    );
    expect(texts('.c-tabs a')).toEqual([
      'People · 7',
      'Invitations · 2',
      'Company login',
      'Company directory',
      'Approvals and rules',
    ]);
    expect(page.querySelector('.c-tabs a[aria-current="page"]')?.textContent.trim()).toBe(
      'People · 7',
    );
    const rows = [...page.querySelectorAll('tr.urow')].map((row) =>
      [...row.querySelectorAll('td')]
        .slice(0, -1)
        .map((cell) => cell.textContent.replace(whitespace, ' ').trim()),
    );
    expect(rows).toEqual([
      [
        'Omar Haddad omar.haddad@harmony.example',
        'Super administrator',
        'On',
        'Active',
        'Oct 29, 2026 09:12 UTC',
        'Sep 1, 2025',
      ],
      [
        'Yousef Karam yousef.karam@harmony.example',
        'Support',
        'Not set',
        'Active',
        'never',
        'Oct 12, 2026',
      ],
    ]);
    expect(texts('tr.urow td:nth-child(3) .chip[data-s="ok"]')).toEqual(['On']);
    expect(texts('tr.urow td:nth-child(3) .chip[data-s="warn"]')).toEqual(['Not set']);
    expect(page.querySelector('tr.urow a.nm-link')?.getAttribute('href')).toBe('/account/u1');
    expect(text('.c-filters .count')).toBe(`${String(TWO)} of ${String(SEVEN)} · by name`);
  });

  it('the search and the three facets go to the store', async () => {
    const input = page.querySelector<HTMLInputElement>('#people-search');
    if (input === null) throw new Error('no search field');
    input.value = 'lina';
    page.querySelector<HTMLFormElement>('form.c-filters')?.requestSubmit();
    expect(store.searched).toEqual(['lina']);
    await pick('State', AccountState.suspended);
    expect(store.states).toEqual([AccountState.suspended]);
    await pick('Two-step', 'exempt');
    expect(store.twoSteps).toEqual(['exempt']);
    await pick('Role', 'r2');
    expect(store.roles).toEqual(['r2']);
  });

  it('New person opens the account creator; the row menu’s Suspend opens the reason form', async () => {
    page.querySelector<HTMLButtonElement>('[data-act="new-person"]')?.click();
    await settle();
    expect(page.querySelector('dialog.drawer[open] hr-account-creator')).not.toBeNull();
    page.querySelector<HTMLButtonElement>('tr.urow [data-act="suspend"]')?.click();
    await settle();
    expect(page.querySelector('hr-account-state-editor')).not.toBeNull();
    expect(texts('.drawer-h h2')).toContain('Suspend account');
  });
});
