import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PersonStore, RolesStore, UsersStore } from '@harmony/administration/data-access';
import {
  AccountSignIn,
  AccountState,
  CompanyLink,
  Device,
  GroupChoice,
  HotelAccessGrant,
  HotelRoleAssignment,
  Passkey,
  PrivilegeLevel,
  Role,
  SecurityEvent,
  UserAccount,
} from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { InstantFormatter, MyProperties } from '@harmony/platform/context';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { PersonPage } from './person-page';

const OK = 200;
const ACCEPTED = 202;
// Runs of whitespace (spaces, newlines) collapse to one space: matches "a \n b" (→ "a b"); rejects "ab" (nothing to do).
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const rana: UserAccount = {
  id: 'u5',
  userName: 'rana@ritz.test',
  email: 'rana@ritz.test',
  displayName: 'Rana Qasem',
  twoFactorEnabled: false,
  twoStepExemptReason: null,
  state: AccountState.active,
  stateReason: null,
  createdDate: '2026-10-24T08:00:00Z',
  roleIds: ['r1'],
};
const frontDesk: Role = {
  id: 'r1',
  code: 'FRONT-DESK',
  nameEn: 'Front desk',
  nameAr: null,
  privilegeLevel: PrivilegeLevel.standard,
  isSystemRole: false,
  isSuperRole: false,
};
const manager: Role = { ...frontDesk, id: 'r2', code: 'MANAGER', nameEn: 'Manager' };
const laptop: Device = {
  id: 'd1',
  label: 'Chrome on Windows',
  firstSeenUtc: '2026-10-24T08:00:00Z',
  lastSeenUtc: '2026-10-24T09:00:00Z',
  lastAddress: '10.0.0.5',
  signedIn: true,
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
  actorSubject: 'u2',
  sourceAddress: '10.0.0.9',
  device: 'Chrome on Windows',
  detail: 'an invitation link was sent',
};

const atAmman: HotelRoleAssignment = {
  id: 'a1',
  roleId: 'r1',
  roleCode: 'FRONT-DESK',
  propertyId: 'p-amman',
  propertyGroupId: null,
  scopeCode: 'AMMAN',
  endsAtUtc: null,
  isCurrent: true,
};
const atLevantEnded: HotelRoleAssignment = {
  ...atAmman,
  id: 'a2',
  propertyId: null,
  propertyGroupId: 'g-levant',
  scopeCode: 'LEVANT',
  endsAtUtc: '2026-10-25T08:00:00Z',
  isCurrent: false,
};
const nightCover: HotelAccessGrant = {
  id: 'g1',
  roleId: 'r2',
  roleCode: 'MANAGER',
  propertyId: 'p-amman',
  propertyGroupId: null,
  scopeCode: 'AMMAN',
  fromUtc: '2026-10-24T20:00:00Z',
  toUtc: '2026-10-25T04:00:00Z',
  revokedAtUtc: null,
  grantedById: 'u2',
  reason: 'Covering the night shift',
  isActive: true,
};

const microsoftLink: CompanyLink = {
  id: 'l1',
  provider: 'Microsoft',
  linkedBy: 'first-sign-in',
  isOpen: true,
  since: '2026-10-27T08:00:00Z',
  until: null,
};
const viaCompany: AccountSignIn = {
  passwordSignIn: false,
  coveredBy: 'Microsoft',
  signsInWith: 'Company',
  links: [microsoftLink],
};

class FakeStore {
  readonly person = signal<UserAccount | null>(rana);
  readonly signInState = signal<AccountSignIn | null>(viaCompany);
  readonly signInProblem = signal<string | null>(null);
  readonly assignmentRows = signal<readonly HotelRoleAssignment[]>([atAmman, atLevantEnded]);
  readonly grantRows = signal<readonly HotelAccessGrant[]>([nightCover]);
  readonly accessProblem = signal<string | null>(null);
  readonly groupRows = signal<readonly GroupChoice[]>([]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  readonly deviceRows = signal<readonly Device[]>([laptop]);
  readonly devicesProblem = signal<string | null>(null);
  readonly passkeyRows = signal<readonly Passkey[]>([key]);
  readonly passkeysProblem = signal<string | null>(null);
  readonly eventRows = signal<readonly SecurityEvent[]>([created]);
  readonly eventsProblem = signal<string | null>(null);
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
  updateRoles(
    _user: UserAccount,
    roleIds: readonly string[],
    k: IntentKey,
  ): Promise<ApiResult<unknown>> {
    this.calls.push(`roles:${roleIds.join('+')}:${String(k.value.length)}`);
    return Promise.resolve(this.answer);
  }
  resetTwoFactor(): Promise<ApiResult<unknown>> {
    this.calls.push('reset');
    return Promise.resolve(this.answer);
  }
  endExemption(): Promise<ApiResult<unknown>> {
    this.calls.push('end-exemption');
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
  loadChoices(): void {
    this.calls.push('choices');
  }
  endAssignment(
    _user: UserAccount,
    assignment: HotelRoleAssignment,
    k: IntentKey,
  ): Promise<ApiResult<unknown>> {
    this.calls.push(`end-assignment:${assignment.id}:${String(k.value.length)}`);
    return Promise.resolve(this.answer);
  }
  revokeGrant(_user: UserAccount, grant: HotelAccessGrant): Promise<ApiResult<unknown>> {
    this.calls.push(`revoke-grant:${grant.id}`);
    return Promise.resolve(this.answer);
  }
  setPasswordSignIn(_user: UserAccount, keep: boolean, k: IntentKey): Promise<ApiResult<unknown>> {
    this.calls.push(`password-sign-in:${String(keep)}:${String(k.value.length)}`);
    return Promise.resolve(this.answer);
  }
  endLink(_user: UserAccount, link: CompanyLink): Promise<ApiResult<unknown>> {
    this.calls.push(`end-link:${link.id}`);
    return Promise.resolve(this.answer);
  }
}

describe('PersonPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<PersonPage>;
  let page: HTMLElement;
  let mayManage = true;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const text = (selector: string): string =>
    page.querySelector(selector)?.textContent.replace(whitespace, ' ').trim() ?? '';
  const click = (selector: string): void => {
    page.querySelector<HTMLElement>(selector)?.click();
  };

  beforeEach(async () => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        { provide: InstantFormatter, useValue: { formatInstant: () => 'Oct 24, 2026, 09:00' } },
        { provide: AccessStore, useValue: { can: () => mayManage } },
        {
          provide: RolesStore,
          useValue: {
            roles: signal([frontDesk, manager]),
            problem: signal(null),
            load: () => undefined,
          },
        },
        { provide: UsersStore, useValue: {} },
        { provide: MyProperties, useValue: { list: signal([]), ready: () => Promise.resolve() } },
      ],
    });
    // The page provides its own PersonStore; the fake takes that slot.
    TestBed.overrideComponent(PersonPage, {
      set: { providers: [{ provide: PersonStore, useValue: store }] },
    });
    fixture = TestBed.createComponent(PersonPage);
    fixture.componentRef.setInput('id', 'u5');
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('names the account to the store, shows who they are, their roles ticked, devices, passkeys and the trail', () => {
    expect(store.opened).toEqual(['u5']);
    expect(text('.person h1')).toBe('Rana Qasem');
    expect(text('[data-two-step] b')).toBe('Not set');
    expect(page.querySelector<HTMLInputElement>('input[data-role="FRONT-DESK"]')?.checked).toBe(
      true,
    );
    expect(page.querySelector<HTMLInputElement>('input[data-role="MANAGER"]')?.checked).toBe(false);
    expect(text('.drow .s-nm b')).toBe('Chrome on Windows');
    expect(text('.krow .s-nm b')).toBe('Work laptop');
    expect(text('.erow .s-nm b')).toBe('account.created');
  });

  it('roles: Save is off until something changes; then the whole set goes with a key; a refusal is worded', async () => {
    const save = page.querySelector<HTMLButtonElement>('[data-act="save-roles"]');
    expect(save?.disabled).toBe(true);
    click('input[data-role="MANAGER"]');
    await settle();
    expect(save?.disabled).toBe(false);
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00077'] };
    click('[data-act="save-roles"]');
    await settle();
    expect(store.calls).toEqual(['roles:r1+r2:36']);
    expect(text('hr-role-assignment [role="alert"]')).toContain('above your own rank');
    expect(store.reloads).toBe(0);
    store.answer = { kind: 'ok', status: OK, data: undefined };
    click('[data-act="save-roles"]');
    await settle();
    expect(store.reloads).toBe(1);
  });

  it('two-step: Exempt opens the reason form; an exempt person gets End exemption; an enrolled one gets Reset', async () => {
    expect(page.querySelector('[data-act="reset-two-step"]')).toBeNull();
    click('[data-act="exempt"]');
    await settle();
    expect(
      page.querySelector('hr-account-state-editor button[type="submit"]')?.textContent,
    ).toContain('Exempt from two-step');
    store.person.set({ ...rana, twoStepExemptReason: 'night desk' });
    await settle();
    expect(text('[data-two-step] b')).toBe('Exempt');
    click('[data-act="end-exemption"]');
    await settle();
    expect(store.calls).toEqual(['end-exemption']);
    store.person.set({ ...rana, twoFactorEnabled: true });
    await settle();
    click('[data-act="reset-two-step"]');
    await settle();
    expect(store.calls).toEqual(['end-exemption', 'reset']);
  });

  it('hotel roles and grants: the rows with where and state; End and Revoke ask first; Give opens the form', async () => {
    expect(text('.arow .s-nm b')).toBe('FRONT-DESK');
    expect(
      [...page.querySelectorAll('.arow')].map((row) => row.getAttribute('data-current')),
    ).toEqual(['true', 'false']);
    expect(page.querySelectorAll('[data-act="end-assignment"]')).toHaveLength(1);
    expect(text('.grow .s-why')).toBe('Covering the night shift');
    click('[data-act="end-assignment"]');
    await settle();
    expect(store.calls).toEqual([]);
    click('dialog[open] .dlg-a .solid');
    await settle();
    expect(store.calls).toEqual(['end-assignment:a1:36']);
    click('[data-act="revoke-grant"]');
    await settle();
    click('dialog[open] .dlg-a .btn:not(.solid)');
    await settle();
    expect(store.calls).toEqual(['end-assignment:a1:36']);
    click('[data-act="assign-role"]');
    await settle();
    expect(page.querySelector('hr-hotel-access-editor #access-days')).not.toBeNull();
    expect(store.calls).toEqual(['end-assignment:a1:36', 'choices']);
  });

  it('sign-in: a company person reads "company account"; Keep on a password is one press; End link asks first', async () => {
    expect(text('[data-sign-in]')).toBe(
      'With their company account; Harmony asks for no password: Microsoft',
    );
    expect(text('[data-link] .s-nm span')).toBe('On their first company sign-in');
    expect(page.querySelector('[data-act="follow-company"]')).toBeNull();
    click('[data-act="keep-password"]');
    await settle();
    expect(store.calls).toEqual(['password-sign-in:true:36']);
    click('[data-act="end-link"]');
    await settle();
    expect(store.calls).toEqual(['password-sign-in:true:36']);
    click('dialog[open] .dlg-a .solid');
    await settle();
    expect(store.calls).toEqual(['password-sign-in:true:36', 'end-link:l1']);
  });

  it('sign-in: a person kept on a password is offered the company sign-in back; a refusal is worded', async () => {
    store.signInState.set({ ...viaCompany, passwordSignIn: true, signsInWith: 'Password' });
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00127'] };
    await settle();
    expect(text('[data-sign-in]')).toBe('With a Harmony password, kept by an administrator.');
    click('[data-act="follow-company"]');
    await settle();
    expect(store.calls).toEqual(['password-sign-in:false:36']);
    expect(text('.empty[role="alert"]')).toContain('Keep at least one administrator');
  });

  it('a device is signed out and a passkey removed with one press each', async () => {
    click('[data-act="sign-out-device"]');
    await settle();
    click('[data-act="remove-passkey"]');
    await settle();
    expect(store.calls).toEqual(['sign-out:d1', 'remove-passkey:k1']);
  });

  it('without ManageUsers: the roles are read-only and no action is offered', async () => {
    mayManage = false;
    fixture.destroy();
    fixture = TestBed.createComponent(PersonPage);
    fixture.componentRef.setInput('id', 'u5');
    page = fixture.nativeElement as HTMLElement;
    await settle();
    expect(page.querySelector<HTMLInputElement>('input[data-role="FRONT-DESK"]')?.disabled).toBe(
      true,
    );
    expect(page.querySelector('[data-act]')).toBeNull();
    mayManage = true;
  });
});
