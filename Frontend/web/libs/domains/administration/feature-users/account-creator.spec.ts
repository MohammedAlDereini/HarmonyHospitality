import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RolesStore, UsersStore } from '@harmony/administration/data-access';
import { NewAccount, PrivilegeLevel, Role } from '@harmony/administration/domain';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { AccountCreator } from './account-creator';

const OK = 200;
const ACCEPTED = 202;
const BAD_REQUEST = 400;
// A UUID as crypto.randomUUID() writes it: 8-4-4-4-12 lower-case hex. Matches "3b241101-e2bb-4255-8caf-4136c566a962";
// rejects "0af7651916cd43dd8448eb211c80319c" (no dashes).
// Tester: https://regex101.com/?regex=%5E%5B0-9a-f-%5D%7B36%7D%24&testString=3b241101-e2bb-4255-8caf-4136c566a962
const uuid = /^[0-9a-f-]{36}$/;

const frontDesk: Role = {
  id: 'r1',
  code: 'FRONT-DESK',
  nameEn: 'Front desk',
  nameAr: null,
  privilegeLevel: PrivilegeLevel.standard,
  isSystemRole: false,
  isSuperRole: false,
};
const manager: Role = {
  ...frontDesk,
  id: 'r2',
  code: 'MANAGER',
  nameEn: 'Manager',
  nameAr: 'مدير',
  privilegeLevel: PrivilegeLevel.manager,
};

@Component({
  selector: 'hr-host',
  imports: [AccountCreator],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-account-creator (done)="closed.set(closed() + 1)" />`,
})
class Host {
  readonly closed = signal(0);
}

describe('hr-account-creator', () => {
  const calls: { account: NewAccount; key: IntentKey }[] = [];
  let answer: ApiResult<unknown>;
  let loads = 0;

  const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

  const render = async () => {
    calls.length = 0;
    loads = 0;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: UsersStore,
          useValue: {
            create: (account: NewAccount, key: IntentKey) => {
              calls.push({ account, key });
              return Promise.resolve(answer);
            },
          },
        },
        {
          provide: RolesStore,
          useValue: {
            roles: signal([frontDesk, manager]),
            loading: signal(false),
            problem: signal(null),
            load: () => {
              loads += 1;
            },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async (): Promise<void> => {
      await tick();
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const type = async (selector: string, text: string): Promise<void> => {
      const box = page.querySelector<HTMLInputElement | HTMLSelectElement>(selector);
      if (box === null) throw new Error(`no ${selector}`);
      box.value = text;
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

  it('the roles are read when the form opens; an empty submit says what is missing and sends nothing', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const creator = await render();
    expect(loads).toBe(1);
    expect(
      [...creator.page.querySelectorAll('#new-role option')].map((o) => o.textContent),
    ).toEqual(['Choose a role', 'Front desk', 'Manager']);
    await creator.send();
    expect(calls).toEqual([]);
    expect(creator.page.querySelector('#new-name-error')?.textContent).toContain('display name');
    expect(creator.page.querySelector('#new-email-error')?.textContent).toContain('email address');
    expect(creator.page.querySelector('#new-role-error')?.textContent).toContain('Choose a role');
  });

  it('a complete form is sent trimmed with the one role and a fresh intent key; success closes it', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const creator = await render();
    await creator.type('#new-name', '  Rana Qasem ');
    await creator.type('#new-email', ' rana@ritz.test ');
    await creator.type('#new-role', 'r2');
    await creator.send();
    expect(calls).toHaveLength(1);
    expect(calls[0]?.account).toEqual({
      displayName: 'Rana Qasem',
      email: 'rana@ritz.test',
      roleId: 'r2',
    });
    expect(calls[0]?.key.value).toMatch(uuid);
    expect(creator.fixture.componentInstance.closed()).toBe(1);
  });

  it('a business refusal is worded from the catalogue and the form stays open', async () => {
    answer = { kind: 'refused', status: ACCEPTED, codes: ['00048'] };
    const creator = await render();
    await creator.type('#new-name', 'Rana Qasem');
    await creator.type('#new-email', 'rana@ritz.test');
    await creator.type('#new-role', 'r1');
    await creator.send();
    expect(creator.page.textContent).toContain('already exists');
    expect(creator.fixture.componentInstance.closed()).toBe(0);
  });

  it("the server's field code lands under its field", async () => {
    answer = { kind: 'invalid', status: BAD_REQUEST, codes: ['10311'] };
    const creator = await render();
    await creator.type('#new-name', 'Rana Qasem');
    await creator.type('#new-email', 'rana@ritz.test');
    await creator.type('#new-role', 'r1');
    await creator.send();
    expect(creator.page.querySelector('#new-email-error')?.textContent).toContain(
      'does not look like',
    );
  });
});
