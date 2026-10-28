import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PersonStore, RolesStore } from '@harmony/administration/data-access';
import {
  AccessScope,
  AccountState,
  GroupChoice,
  PrivilegeLevel,
  Role,
  UserAccount,
} from '@harmony/administration/domain';
import { MyProperties, PropertySummary } from '@harmony/platform/context';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { AccessMode, HotelAccessEditor, scopeOf } from './hotel-access-editor';

const OK = 200;
const ACCEPTED = 202;
const BAD_REQUEST = 400;
const EIGHT_HOURS = 8;
const THREE_DAYS = 3;
const MS_PER_HOUR = 3_600_000;
const HOURS_PER_DAY = 24;
/** The instant sent may lag the one computed here by the test's own run time: a minute is plenty. */
const SLACK_MS = 60_000;

const lina: UserAccount = {
  id: 'u7',
  userName: 'lina@ritz.test',
  email: 'lina@ritz.test',
  displayName: 'Lina Haddad',
  twoFactorEnabled: false,
  twoStepExemptReason: null,
  state: AccountState.active,
  stateReason: null,
  createdDate: '2026-10-24T08:00:00Z',
  isDirectoryManaged: false,
  roleIds: [],
};
const reader: Role = {
  id: 'r9',
  code: 'HOTEL-READER',
  nameEn: 'Hotel reader',
  nameAr: null,
  privilegeLevel: PrivilegeLevel.standard,
  isSystemRole: false,
  isSuperRole: false,
};
const amman: PropertySummary = {
  id: 'p-amman',
  code: 'AMMAN',
  nameEn: 'Ritz Amman',
  nameAr: 'ريتز عمّان',
  timeZoneId: 'Asia/Amman',
  currency: 'JOD',
};
const levant: GroupChoice = {
  id: 'g-levant',
  code: 'LEVANT',
  typeCode: 'Region',
  nameEn: 'Levant',
  nameAr: 'بلاد الشام',
};

interface Call {
  readonly kind: AccessMode;
  readonly roleId: string;
  readonly scope: AccessScope;
  readonly until: string | null;
  readonly reason: string | null;
  readonly key: IntentKey;
}

class FakePerson {
  readonly groupRows = signal<readonly GroupChoice[]>([levant]);
  readonly calls: Call[] = [];
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  choicesWanted = 0;
  loadChoices(): void {
    this.choicesWanted += 1;
  }
  assignRole(
    _user: UserAccount,
    roleId: string,
    scope: AccessScope,
    endsAtUtc: string | null,
    key: IntentKey,
  ): Promise<ApiResult<unknown>> {
    this.calls.push({ kind: 'assign', roleId, scope, until: endsAtUtc, reason: null, key });
    return Promise.resolve(this.answer);
  }
  grantAccess(
    _user: UserAccount,
    roleId: string,
    scope: AccessScope,
    toUtc: string,
    reason: string,
    key: IntentKey,
  ): Promise<ApiResult<unknown>> {
    this.calls.push({ kind: 'grant', roleId, scope, until: toUtc, reason, key });
    return Promise.resolve(this.answer);
  }
}

@Component({
  selector: 'hr-host',
  imports: [HotelAccessEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-hotel-access-editor
    [user]="user"
    [mode]="mode()"
    (done)="closed.set(closed() + 1)"
    (cancelled)="gaveUp.set(gaveUp() + 1)"
  />`,
})
class Host {
  readonly user = lina;
  readonly mode = signal<AccessMode>('assign');
  readonly closed = signal(0);
  readonly gaveUp = signal(0);
}

describe('hr-hotel-access-editor', () => {
  let person: FakePerson;

  const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

  const render = async (mode: AccessMode = 'assign') => {
    person = new FakePerson();
    TestBed.configureTestingModule({
      providers: [
        { provide: PersonStore, useValue: person },
        {
          provide: RolesStore,
          useValue: { roles: signal([reader]), problem: signal(null), load: () => undefined },
        },
        {
          provide: MyProperties,
          useValue: { list: signal([amman]), ready: () => Promise.resolve() },
        },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.mode.set(mode);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async (): Promise<void> => {
      await tick();
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const choose = async (id: string, value: string): Promise<void> => {
      const box = page.querySelector<HTMLSelectElement>(`#${id}`);
      if (box === null) throw new Error(`no ${id}`);
      box.value = value;
      box.dispatchEvent(new Event('input', { bubbles: true }));
      box.dispatchEvent(new Event('change', { bubbles: true }));
      await settle();
    };
    const type = async (id: string, value: string): Promise<void> => {
      const box = page.querySelector<HTMLInputElement>(`#${id}`);
      if (box === null) throw new Error(`no ${id}`);
      box.value = value;
      box.dispatchEvent(new Event('input', { bubbles: true }));
      await settle();
    };
    const submit = async (): Promise<void> => {
      page.querySelector('form')?.dispatchEvent(new Event('submit', { cancelable: true }));
      await settle();
    };
    return { fixture, page, settle, choose, type, submit };
  };

  it('offers the roles, my hotels and the customer groups; nothing goes until a role and a place are chosen', async () => {
    const { page, submit } = await render();
    expect(person.choicesWanted).toBe(1);
    expect(
      [...page.querySelectorAll('#access-role option')].map((o) => o.textContent.trim()),
    ).toEqual(['Choose a role', 'Hotel reader']);
    expect(
      [...page.querySelectorAll('#access-where optgroup')].map((g) => g.getAttribute('label')),
    ).toEqual(['Hotels', 'Groups']);
    expect(page.querySelector('#access-days')).not.toBeNull();
    expect(page.querySelector('#access-hours')).toBeNull();
    await submit();
    expect(person.calls).toEqual([]);
    expect(page.querySelector('#access-role-error')?.textContent).toContain('Choose a role');
    expect(page.querySelector('#access-where-error')?.textContent).toContain(
      'one hotel or one group',
    );
  });

  it('assign: the role at a hotel, open-ended, with a fresh key; then at a group, ending after three days', async () => {
    const { fixture, choose, type, submit } = await render();
    await choose('access-role', 'r9');
    await choose('access-where', 'h:p-amman');
    await submit();
    expect(person.calls).toHaveLength(1);
    expect(person.calls[0]).toMatchObject({
      kind: 'assign',
      roleId: 'r9',
      scope: { propertyId: 'p-amman', propertyGroupId: null },
      until: null,
    });
    expect(person.calls[0]?.key.value).not.toBe('');
    expect(fixture.componentInstance.closed()).toBe(1);
    await choose('access-where', 'g:g-levant');
    await type('access-days', String(THREE_DAYS));
    const before = Date.now();
    await submit();
    const sent = person.calls[1];
    expect(sent?.scope).toEqual({ propertyId: null, propertyGroupId: 'g-levant' });
    const until = new Date(sent?.until ?? '').getTime();
    expect(until).toBeGreaterThanOrEqual(before + THREE_DAYS * HOURS_PER_DAY * MS_PER_HOUR);
    expect(until).toBeLessThan(before + THREE_DAYS * HOURS_PER_DAY * MS_PER_HOUR + SLACK_MS);
  });

  it('grant: hours and a reason are required; eight hours from now goes with the reason; 10334 lands under hours', async () => {
    const { page, choose, type, submit } = await render('grant');
    expect(page.querySelector('#access-days')).toBeNull();
    await choose('access-role', 'r9');
    await choose('access-where', 'h:p-amman');
    await submit();
    expect(person.calls).toEqual([]);
    expect(page.querySelector('#access-hours-error')?.textContent).toContain('at least one hour');
    expect(page.querySelector('#access-reason-error')?.textContent).toContain('Give a reason');
    await type('access-hours', String(EIGHT_HOURS));
    await type('access-reason', ' Covering the night shift ');
    const before = Date.now();
    await submit();
    expect(person.calls).toHaveLength(1);
    expect(person.calls[0]).toMatchObject({
      kind: 'grant',
      roleId: 'r9',
      scope: { propertyId: 'p-amman', propertyGroupId: null },
      reason: 'Covering the night shift',
    });
    const until = new Date(person.calls[0]?.until ?? '').getTime();
    expect(until).toBeGreaterThanOrEqual(before + EIGHT_HOURS * MS_PER_HOUR);
    person.answer = { kind: 'invalid', status: BAD_REQUEST, codes: ['10334'] };
    await submit();
    expect(page.querySelector('#access-hours-error')?.textContent).toContain('30 days at most');
  });

  it('a business refusal is worded on the general line; Cancel tells the drawer', async () => {
    const { fixture, page, choose, submit } = await render();
    person.answer = { kind: 'refused', status: ACCEPTED, codes: ['00105'] };
    await choose('access-role', 'r9');
    await choose('access-where', 'h:p-amman');
    await submit();
    expect(page.querySelector('.editor > [role="alert"]')?.textContent).toContain('already given');
    expect(fixture.componentInstance.closed()).toBe(0);
    page.querySelector<HTMLButtonElement>('button[type="button"].btn')?.click();
    expect(fixture.componentInstance.gaveUp()).toBe(1);
  });

  it('scopeOf reads the Where choice back into exactly one id', () => {
    expect(scopeOf('h:p-amman')).toEqual({ propertyId: 'p-amman', propertyGroupId: null });
    expect(scopeOf('g:g-levant')).toEqual({ propertyId: null, propertyGroupId: 'g-levant' });
  });
});
