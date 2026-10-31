import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PersonStore, RolesStore, UsersStore } from '@harmony/administration/data-access';
import {
  AccountSignIn,
  AccountState,
  Device,
  HotelAccessGrant,
  HotelRoleAssignment,
  Passkey,
  PrivilegeLevel,
  Role,
  SecurityEvent,
  SessionRecord,
  UserAccount,
} from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { MyProperties } from '@harmony/platform/context';
import { ApiResult } from '@harmony/platform/http';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { PersonConsolePage } from './person-console-page';

const OK = 200;
const THREE = 3;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const lina: UserAccount = {
  id: 'u2',
  userName: 'lina.nasser@harmony.example',
  email: 'lina.nasser@harmony.example',
  displayName: 'Lina Nasser',
  twoFactorEnabled: true,
  twoStepExemptReason: null,
  state: AccountState.active,
  stateReason: null,
  isDirectoryManaged: false,
  createdDate: '2025-09-03T08:00:00',
  lastSignInUtc: '2026-10-29T08:41:00Z',
  lastSignInDevice: 'Windows · Edge 130',
  roleIds: ['r1', 'r2'],
};
const omar: UserAccount = { ...lina, id: 'u1', displayName: 'Omar Haddad', roleIds: ['r1'] };
const superAdministrator: Role = {
  id: 'r1',
  code: 'SUPERADMIN',
  nameEn: 'Super administrator',
  nameAr: null,
  privilegeLevel: PrivilegeLevel.standard,
  isSystemRole: true,
  isSuperRole: true,
};
const operations: Role = {
  ...superAdministrator,
  id: 'r2',
  code: 'PLATFORM-OPS',
  nameEn: 'Platform operations',
  isSuperRole: false,
};
const edge: Device = {
  id: 'd1',
  label: 'Windows · Edge 130',
  firstSeenUtc: '2026-09-01T08:00:00Z',
  lastSeenUtc: '2026-10-29T08:41:00Z',
  lastAddress: '185.62.14.7',
  signedIn: true,
  trustedAtUtc: '2026-09-01T08:00:00Z',
};
const phone: Device = {
  ...edge,
  id: 'd2',
  label: 'iPhone · Safari',
  lastSeenUtc: '2026-10-21T18:12:00Z',
  lastAddress: '94.249.2.18',
  signedIn: false,
};
const chrome: Device = {
  ...edge,
  id: 'd3',
  label: 'Windows · Chrome 129',
  lastSeenUtc: '2026-09-30T09:05:00Z',
  lastAddress: '212.35.90.4',
  signedIn: false,
  trustedAtUtc: null,
};
const key: Passkey = {
  id: 'k1',
  name: 'Work laptop',
  createdUtc: '2026-10-24T08:00:00Z',
  lastUsedUtc: null,
  isBackedUp: true,
};
const created: SecurityEvent = {
  action: 'account.created',
  occurredAtUtc: '2026-10-24T08:00:00Z',
  actorSubject: 'u1',
  sourceAddress: '10.0.0.9',
  device: 'Chrome on Windows',
  detail: 'an invitation link was sent',
};
const session: SessionRecord = {
  startedAtUtc: '2026-10-29T08:41:00Z',
  endedAtUtc: null,
  endReason: null,
  methods: 'pwd mfa',
  provider: 'local',
  multiFactorSatisfied: true,
};
const migration: HotelAccessGrant = {
  id: 'g1',
  roleId: 'r1',
  roleCode: 'SUPERADMIN',
  propertyId: null,
  propertyGroupId: 'grp',
  scopeCode: 'LEV',
  fromUtc: '2026-10-27T10:00:00Z',
  toUtc: '2026-11-02T18:00:00Z',
  revokedAtUtc: null,
  grantedById: 'u1',
  reason: 'Migration support for the Levant go-live weekend',
  isActive: true,
};
const atAmman: HotelRoleAssignment = {
  id: 'a1',
  roleId: 'r2',
  roleCode: 'PLATFORM-OPS',
  propertyId: 'p1',
  propertyGroupId: null,
  scopeCode: 'AMM-GRD',
  endsAtUtc: null,
  isCurrent: true,
};
const password: AccountSignIn = {
  passwordSignIn: false,
  coveredBy: null,
  signsInWith: 'Password',
  links: [],
};

class FakeStore {
  readonly person = signal<UserAccount | null>(lina);
  readonly signInState = signal<AccountSignIn | null>(password);
  readonly signInProblem = signal<string | null>(null);
  readonly assignmentRows = signal<readonly HotelRoleAssignment[]>([atAmman]);
  readonly grantRows = signal<readonly HotelAccessGrant[]>([migration]);
  readonly accessProblem = signal<string | null>(null);
  readonly groupRows = signal([]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  readonly deviceRows = signal<readonly Device[]>([edge, phone, chrome]);
  readonly devicesProblem = signal<string | null>(null);
  readonly passkeyRows = signal<readonly Passkey[]>([key]);
  readonly passkeysProblem = signal<string | null>(null);
  readonly eventRows = signal<readonly SecurityEvent[]>([created]);
  readonly eventsProblem = signal<string | null>(null);
  readonly sessionRows = signal<readonly SessionRecord[]>([session]);
  opened: string[] = [];
  reloads = 0;
  calls: string[] = [];
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  open(id: string): void {
    this.opened.push(id);
  }
  reload(): void {
    this.reloads += 1;
  }
  loadChoices(): void {
    this.calls.push('choices');
  }
  resetTwoFactor(): Promise<ApiResult<unknown>> {
    this.calls.push('reset');
    return Promise.resolve(this.answer);
  }
  signOutDevice(_user: UserAccount, device: Device): Promise<ApiResult<unknown>> {
    this.calls.push(`sign-out:${device.id}`);
    return Promise.resolve(this.answer);
  }
  removePasskey(_user: UserAccount, passkey: Passkey): Promise<ApiResult<unknown>> {
    this.calls.push(`remove-passkey:${passkey.id}`);
    return Promise.resolve(this.answer);
  }
  revokeGrant(_user: UserAccount, grant: HotelAccessGrant): Promise<ApiResult<unknown>> {
    this.calls.push(`revoke-grant:${grant.id}`);
    return Promise.resolve(this.answer);
  }
}

describe('PersonConsolePage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<PersonConsolePage>;
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
  const mount = async (tab?: string): Promise<void> => {
    fixture = TestBed.createComponent(PersonConsolePage);
    fixture.componentRef.setInput('id', 'u2');
    if (tab !== undefined) fixture.componentRef.setInput('tab', tab);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  };

  beforeEach(() => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: AccessStore, useValue: { can: () => true } },
        {
          provide: RolesStore,
          useValue: {
            roles: signal([superAdministrator, operations]),
            problem: signal(null),
            loading: signal(false),
            load: () => undefined,
          },
        },
        { provide: UsersStore, useValue: { rows: signal([omar, lina]) } },
        { provide: MyProperties, useValue: { list: signal([]), ready: () => Promise.resolve() } },
      ],
    });
    TestBed.overrideComponent(PersonConsolePage, {
      set: { providers: [{ provide: PersonStore, useValue: store }] },
    });
  });

  it('names the account to the store; the head says who they are, how they sign in and when they last did', async () => {
    await mount();
    expect(store.opened).toEqual(['u2']);
    expect(text('.person h1')).toContain('Lina Nasser');
    expect(text('.c-av.big')).toBe('LN');
    expect(text('[data-status]')).toBe('Active');
    expect(text('[data-two-step] b')).toBe('Two-step on');
    expect(text('.lead')).toBe(
      'lina.nasser@harmony.example · Super administrator, Platform operations · Harmony password · last sign-in Oct 29, 2026 08:41 UTC from Windows · Edge 130',
    );
    expect(texts('.c-tabs [role="tab"]')).toEqual([
      'Overview',
      'Devices · 3',
      'Passkeys · 1',
      'Sign-ins · 1',
      'Security trail · 1',
      'Hotel roles · 1',
    ]);
  });

  it('the overview holds the roles editor, the sign-in facts, the access lent for a while and the devices', async () => {
    await mount();
    expect(page.querySelector('.roles-card hr-role-assignment')).not.toBeNull();
    expect(texts('.cards .c-card:nth-child(2) .c-facts dd')).toEqual([
      'Set up; a code is asked at each sign-in',
      'Harmony password',
      'None linked',
      '2 of 3',
    ]);
    expect(texts('.cards .c-card:nth-child(2) .acts .btn')).toEqual([
      'Reset two-step',
      'Exempt from two-step',
    ]);
    expect(texts('[data-grant] dt')).toEqual(['SUPERADMIN at LEV', 'Reason', 'Given by']);
    expect(texts('[data-grant] dd')).toEqual([
      'until Nov 2, 2026 18:00 UTC',
      'Migration support for the Levant go-live weekend',
      'Omar Haddad · Oct 27, 2026',
    ]);
    const devices = [...page.querySelectorAll('tr.drow')].map((row) =>
      [...row.querySelectorAll('td')].map((cell) =>
        cell.textContent.replace(whitespace, ' ').trim(),
      ),
    );
    expect(devices).toEqual([
      [
        'Windows · Edge 130',
        '185.62.14.7',
        'Trusted',
        'Signed in',
        'Oct 29, 2026 08:41 UTC',
        'Sign out',
      ],
      ['iPhone · Safari', '94.249.2.18', 'Trusted', 'Signed out', 'Oct 21, 2026 18:12 UTC', ''],
      [
        'Windows · Chrome 129',
        '212.35.90.4',
        'Not trusted',
        'Signed out',
        'Sep 30, 2026 09:05 UTC',
        '',
      ],
    ]);
    expect(page.querySelectorAll('tr.drow').length).toBe(THREE);
    page.querySelector<HTMLButtonElement>('[data-act="sign-out-device"]')?.click();
    await settle();
    expect(store.calls).toEqual(['sign-out:d1']);
  });

  it('opens on the tab the route asks for; the trail and the hotel roles are tables', async () => {
    await mount('trail');
    expect(page.querySelector('[role="tab"][aria-selected="true"]')?.textContent.trim()).toBe(
      'Security trail · 1',
    );
    expect(texts('tr.erow td')).toEqual([
      'account.created an invitation link was sent',
      'Oct 24, 2026 08:00 UTC',
      '10.0.0.9',
      'Chrome on Windows',
    ]);
    page.querySelector<HTMLButtonElement>('[role="tab"]:nth-child(6)')?.click();
    await settle();
    expect(texts('tr.arow td')).toEqual([
      'PLATFORM-OPS AMM-GRD',
      'Hotel',
      'No end',
      'Current',
      'End',
    ]);
    expect(page.querySelector('[data-act="assign-role"]')).not.toBeNull();
  });

  it('Suspend opens the reason form in the drawer; Grant opens the access editor', async () => {
    await mount();
    page.querySelector<HTMLButtonElement>('.ph-acts [data-act="suspend"]')?.click();
    await settle();
    expect(page.querySelector('hr-account-state-editor')).not.toBeNull();
    expect(texts('.drawer-h h2')).toContain('Suspend account');
    page.querySelector<HTMLButtonElement>('[data-act="grant-access"]')?.click();
    await settle();
    expect(page.querySelector('hr-hotel-access-editor')).not.toBeNull();
  });
});
