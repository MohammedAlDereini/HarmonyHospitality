import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RolesStore, UsersStore } from '@harmony/administration/data-access';
import { AccountState, UserAccount, UserFilters } from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { InstantFormatter } from '@harmony/platform/context';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { PageActions } from '@harmony/ui/shell';
import { UsersPage } from './users-page';

const OK = 200;
const TOTAL = 60;
const SECOND_PAGE = 2;
const LAST_PAGE = 3;
const TWO_STEP_CELL = 1;
const CREATED_CELL = 3;
// Runs of whitespace (spaces, newlines) collapse to one space: matches "a \n b" (→ "a b"); rejects "ab" (nothing to do).
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const ahmad: UserAccount = {
  id: 'u1',
  userName: 'ahmad@ritz.test',
  email: 'ahmad@ritz.test',
  displayName: 'Ahmad Saleh',
  twoFactorEnabled: true,
  twoStepExemptReason: null,
  lastSignInUtc: null,
  lastSignInDevice: null,
  state: AccountState.active,
  stateReason: null,
  createdDate: '2026-10-24T08:00:00Z',
  isDirectoryManaged: false,
  roleIds: ['r1'],
};
const lina: UserAccount = {
  ...ahmad,
  id: 'u2',
  userName: 'lina@ritz.test',
  email: null,
  displayName: 'Lina Haddad',
  twoFactorEnabled: false,
  twoStepExemptReason: 'night desk',
  lastSignInUtc: null,
  lastSignInDevice: null,
  state: AccountState.suspended,
};

class FakeStore {
  readonly filters = signal<UserFilters>({
    search: '',
    state: null,
    twoStep: null,
    roleId: null,
    page: 1,
  });
  readonly rows = signal<readonly UserAccount[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  searched: string[] = [];
  pages: number[] = [];
  reloads = 0;
  reinstated: { id: string; key: IntentKey }[] = [];
  search(text: string): void {
    this.searched.push(text);
  }
  goTo(page: number): void {
    this.pages.push(page);
  }
  reload(): void {
    this.reloads += 1;
  }
  reinstate(user: UserAccount, key: IntentKey): Promise<ApiResult<unknown>> {
    this.reinstated.push({ id: user.id, key });
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
}

describe('UsersPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<UsersPage>;
  let page: HTMLElement;
  let mayManage = true;

  const render = async (): Promise<void> => {
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const text = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) => cell.textContent.trim());

  beforeEach(async () => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: UsersStore, useValue: store },
        {
          provide: RolesStore,
          useValue: {
            roles: signal([]),
            loading: signal(false),
            problem: signal(null),
            load: () => undefined,
          },
        },
        { provide: InstantFormatter, useValue: { formatInstant: () => 'Oct 24, 2026' } },
        { provide: AccessStore, useValue: { can: () => mayManage } },
      ],
    });
    fixture = TestBed.createComponent(UsersPage);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  it('lists the accounts: name, email or user name, two-step, state, created', async () => {
    store.rows.set([ahmad, lina]);
    store.total.set(TOTAL);
    await render();
    const first = 'tr.urow:first-child';
    const last = 'tr.urow:last-child';
    expect(text('thead th')).toEqual(['Name', 'Two-step', 'State', 'Created', 'Actions']);
    expect(text(`${first} a.nm-link, ${first} .sub`)).toEqual(['Ahmad Saleh', 'ahmad@ritz.test']);
    expect(text(`${first} td`).slice(TWO_STEP_CELL, CREATED_CELL)).toEqual(['On', 'Active']);
    expect(text(`${first} td.num`)).toEqual(['Oct 24, 2026']);
    expect(text(`${last} a.nm-link, ${last} .sub`)).toEqual(['Lina Haddad', 'lina@ritz.test']);
    expect(text(`${last} td`).slice(TWO_STEP_CELL, CREATED_CELL)).toEqual(['Exempt', 'Suspended']);
    expect(page.querySelector(`${last} .pill[data-s="warn"]`)).not.toBeNull();
    expect(page.querySelector('hr-pager')?.textContent.replace(whitespace, ' ').trim()).toBe(
      '60 rows Previous Page 1 of 3 Next',
    );
  });

  it('paging: Previous is off on the first page, Next is off on the last', async () => {
    store.rows.set([ahmad]);
    store.total.set(TOTAL);
    await render();
    const [previous, next] = [...page.querySelectorAll<HTMLButtonElement>('hr-pager .btn')];
    expect(previous?.disabled).toBe(true);
    expect(next?.disabled).toBe(false);
    next?.click();
    expect(store.pages).toEqual([SECOND_PAGE]);
    store.filters.set({ search: '', state: null, twoStep: null, roleId: null, page: LAST_PAGE });
    await render();
    expect(page.querySelectorAll<HTMLButtonElement>('hr-pager .btn')[1]?.disabled).toBe(true);
  });

  it('a search is sent as typed; nothing matching says so', () => {
    const input = page.querySelector<HTMLInputElement>('#users-search');
    if (input === null) throw new Error('no search field');
    input.value = '  lina ';
    page.querySelector('form')?.requestSubmit();
    expect(store.searched).toEqual(['  lina ']);
    expect(page.textContent).toContain('No accounts match');
  });

  it('a failed read shows the sentence and Try again', async () => {
    store.problem.set('Harmony is not responding. Try again in a moment.');
    await render();
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('not responding');
    page.querySelector<HTMLButtonElement>('[role="alert"] button')?.click();
    expect(store.reloads).toBe(1);
  });

  it('with ManageUsers: Suspend opens the reason drawer for that row; Reinstate asks the store with a fresh key', async () => {
    store.rows.set([ahmad, lina]);
    store.total.set(TOTAL);
    await render();
    expect(text('.tlink')).toEqual(['Suspend', 'Terminate', 'Reinstate', 'Terminate']);
    page.querySelector<HTMLButtonElement>('tr.urow:first-child [data-act="suspend"]')?.click();
    await render();
    const stateDrawer = page.querySelectorAll<HTMLDialogElement>('dialog.drawer')[1];
    expect(stateDrawer?.hasAttribute('open')).toBe(true);
    expect(stateDrawer?.querySelector('.drawer-h h2')?.textContent).toBe('Suspend account');
    expect(stateDrawer?.querySelector('hr-account-state-editor')).not.toBeNull();
    stateDrawer?.querySelector<HTMLButtonElement>('.drawer-x')?.click();
    await render();
    expect(page.querySelector('hr-account-state-editor')).toBeNull();
    page.querySelector<HTMLButtonElement>('tr.urow:last-child [data-act="reinstate"]')?.click();
    await render();
    expect(store.reinstated.map((call) => call.id)).toEqual(['u2']);
    expect(store.reinstated[0]?.key.value).not.toBe('');
  });

  it('Terminate opens the drawer in its final mode; a terminated account offers nothing', async () => {
    const gone: UserAccount = { ...lina, id: 'u3', state: AccountState.terminated };
    store.rows.set([ahmad, gone]);
    await render();
    expect(text('tr.urow:last-child .tlink')).toEqual([]);
    page.querySelector<HTMLButtonElement>('tr.urow:first-child [data-act="terminate"]')?.click();
    await render();
    expect(
      page.querySelectorAll('dialog.drawer')[1]?.querySelector('.drawer-h h2')?.textContent,
    ).toBe('Terminate account');
    const editor = page.querySelector('dialog.drawer hr-account-state-editor');
    expect(editor?.querySelector('.warning')?.textContent).toContain('cannot be undone');
    expect(editor?.querySelector('button[type="submit"]')?.textContent.trim()).toBe(
      'Terminate account',
    );
  });

  it('a person from the company directory offers no state action: the directory changes them', async () => {
    store.rows.set([{ ...ahmad, isDirectoryManaged: true }, lina]);
    await render();
    expect(text('tr.urow:first-child .tlink')).toEqual([]);
    expect(text('tr.urow:last-child .tlink')).toEqual(['Reinstate', 'Terminate']);
  });

  it('the head links lead to company login, the directory, invitations and the rules', () => {
    const links = [...page.querySelectorAll<HTMLAnchorElement>('.head-links a')];
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/company-login',
      '/directory',
      '/invitations',
      '/security-rules',
    ]);
  });

  it('without ManageUsers: no action buttons at all', async () => {
    mayManage = false;
    store.rows.set([ahmad, lina]);
    await render();
    expect(page.querySelector('.tlink')).toBeNull();
    mayManage = true;
  });

  it('the page offers Add user to the context bar; pressing it opens the new-account form above the rows', async () => {
    store.rows.set([ahmad]);
    await render();
    const offered = TestBed.inject(PageActions).template();
    if (offered === null) throw new Error('nothing offered to the slot');
    const slot = document.createElement('div');
    const view = offered.createEmbeddedView(undefined);
    view.detectChanges();
    view.rootNodes.forEach((node: Node) => slot.appendChild(node));
    const button = slot.querySelector<HTMLButtonElement>('.pbtn');
    expect(button?.textContent.trim()).toBe('Add user');
    expect(page.querySelector('hr-account-creator')).toBeNull();
    button?.click();
    await render();
    expect(page.querySelector('hr-account-creator')).not.toBeNull();
    page.querySelector<HTMLButtonElement>('hr-account-creator .btn:not(.solid)')?.click();
    await render();
    expect(page.querySelector('hr-account-creator')).toBeNull();
    view.destroy();
  });
});
