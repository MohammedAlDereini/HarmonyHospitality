import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RolesStore, UsersStore } from '@harmony/administration/data-access';
import {
  AccountState,
  PermissionEntry,
  PrivilegeLevel,
  Role,
  RoleDetail,
  UserAccount,
} from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult } from '@harmony/platform/http';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { RolesConsolePage } from './roles-console-page';

const OK = 200;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const superAdministrator: Role = {
  id: 'r0',
  code: 'SUPERADMIN',
  nameEn: 'SUPERADMIN',
  nameAr: null,
  privilegeLevel: PrivilegeLevel.administrator,
  isSystemRole: true,
  isSuperRole: true,
};
const operations: Role = {
  ...superAdministrator,
  id: 'r1',
  code: 'PLATFORM-OPS',
  nameEn: 'Platform operations',
  privilegeLevel: PrivilegeLevel.manager,
  isSystemRole: false,
  isSuperRole: false,
};
const support: Role = { ...operations, id: 'r2', code: 'SUPPORT', nameEn: 'Support (read-only)' };
const viewTenants: PermissionEntry = {
  id: 'p1',
  permissionValue: 'ViewTenants',
  name: 'View tenants',
  description: 'See every customer and its hotels',
};
const manageTenants: PermissionEntry = {
  id: 'p2',
  permissionValue: 'ManageTenants',
  name: 'Manage tenants',
  description: null,
};
const viewUsers: PermissionEntry = {
  id: 'p3',
  permissionValue: 'ViewUsers',
  name: 'View users',
  description: 'See platform people',
};
const held: Readonly<Record<string, readonly PermissionEntry[]>> = {
  r1: [viewTenants, manageTenants, viewUsers],
  r2: [viewTenants, viewUsers],
};
const person = (id: string, name: string, roleIds: readonly string[]): UserAccount => ({
  id,
  userName: `${id}@harmony.example`,
  email: `${id}@harmony.example`,
  displayName: name,
  twoFactorEnabled: true,
  twoStepExemptReason: null,
  state: AccountState.active,
  stateReason: null,
  isDirectoryManaged: false,
  createdDate: '2026-09-01T08:00:00',
  lastSignInUtc: null,
  lastSignInDevice: null,
  roleIds,
});

class FakeStore {
  readonly roles = signal<readonly Role[]>([superAdministrator, operations, support]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  readonly permissions = signal<readonly PermissionEntry[]>([
    viewTenants,
    manageTenants,
    viewUsers,
  ]);
  readonly catalogueLoading = signal(false);
  readonly catalogueProblem = signal<string | null>(null);
  calls: string[] = [];
  load(): void {
    this.calls.push('load');
  }
  loadCatalogue(): void {
    this.calls.push('catalogue');
  }
  detail(role: Role): Promise<ApiResult<RoleDetail>> {
    return Promise.resolve({
      kind: 'ok',
      status: OK,
      data: { ...role, permissions: held[role.id] ?? [] },
    });
  }
  setPermissions(role: Role, ids: readonly string[]): Promise<ApiResult<unknown>> {
    this.calls.push(`set:${role.code}:${[...ids].sort().join('+')}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
  remove(role: Role): Promise<ApiResult<unknown>> {
    this.calls.push(`remove:${role.code}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
}

describe('RolesConsolePage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<RolesConsolePage>;
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
  const box = (role: string, permission: string): HTMLInputElement => {
    const found = page.querySelector<HTMLInputElement>(
      `input[data-role="${role}"][data-permission="${permission}"]`,
    );
    if (found === null) throw new Error(`no cell ${role} ${permission}`);
    return found;
  };

  beforeEach(async () => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: RolesStore, useValue: store },
        {
          provide: UsersStore,
          useValue: {
            everyone: signal([
              person('u1', 'Omar Haddad', ['r0']),
              person('u2', 'Lina Nasser', ['r1', 'r0']),
              person('u3', 'Sami Aoun', ['r1']),
              person('u4', 'Yousef Karam', ['r2']),
            ]),
            countAll: () => undefined,
          },
        },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    fixture = TestBed.createComponent(RolesConsolePage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
    await settle();
  });

  it('draws one column per role with its note, one row per permission by area, ticks for the system role and boxes for the others', () => {
    expect(store.calls).toEqual(['load', 'catalogue']);
    expect(text('.lead')).toBe(
      'Platform tenant · 3 roles · 4 people · changes publish to Redis at once',
    );
    expect(texts('th.role .t')).toEqual([
      'SUPERADMIN',
      'Platform operations',
      'Support (read-only)',
    ]);
    expect(texts('th.role .s')).toEqual([
      'System · locked',
      'Custom · 2 people',
      'Custom · 1 person',
    ]);
    expect(texts('tr.group th')).toEqual(['Tenants · platform only', 'People']);
    expect(texts('tr.prow td.perm')).toEqual([
      'ViewTenants See every customer and its hotels',
      'ManageTenants Manage tenants',
      'ViewUsers See platform people',
    ]);
    expect(page.querySelectorAll('tr.prow td.grant .tick').length).toBe(store.permissions().length);
    expect(box('PLATFORM-OPS', 'ManageTenants').checked).toBe(true);
    expect(box('SUPPORT', 'ManageTenants').checked).toBe(false);
    expect(text('[data-role-card] h3')).toBe('Platform operations 2');
    expect(texts('[data-role-card] .people .nm')).toEqual(['Lina Nasser', 'Sami Aoun']);
    expect(page.querySelector('[data-changes]')).toBeNull();
  });

  it('ticking a box counts a change and says it; Save sends each changed role once; Discard forgets', async () => {
    box('SUPPORT', 'ManageTenants').click();
    await settle();
    expect(text('[data-changes] b')).toBe('1 change not saved');
    expect(text('[data-changes] .note')).toBe('Support (read-only) gains ManageTenants');
    box('PLATFORM-OPS', 'ViewUsers').click();
    await settle();
    expect(text('[data-changes] b')).toBe('2 changes not saved');
    page.querySelector<HTMLButtonElement>('[data-act="save-changes"]')?.click();
    await settle();
    await settle();
    expect(store.calls.filter((call) => call.startsWith('set:'))).toEqual([
      'set:SUPPORT:p1+p2+p3',
      'set:PLATFORM-OPS:p1+p2',
    ]);
    expect(page.querySelector('[data-changes]')).toBeNull();
    box('SUPPORT', 'ViewTenants').click();
    await settle();
    page.querySelector<HTMLButtonElement>('[data-act="discard"]')?.click();
    await settle();
    expect(page.querySelector('[data-changes]')).toBeNull();
    expect(box('SUPPORT', 'ViewTenants').checked).toBe(true);
  });

  it('New role opens the editor; choosing a column shows its people', async () => {
    page.querySelector<HTMLButtonElement>('[data-act="new-role"]')?.click();
    await settle();
    expect(page.querySelector('dialog.drawer[open] hr-role-editor')).not.toBeNull();
    page.querySelectorAll<HTMLButtonElement>('th.role .pick')[2]?.click();
    await settle();
    expect(texts('[data-role-card] .people .nm')).toEqual(['Yousef Karam']);
  });
});
