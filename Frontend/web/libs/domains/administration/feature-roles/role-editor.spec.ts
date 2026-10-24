import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RolesStore } from '@harmony/administration/data-access';
import { PermissionEntry, PrivilegeLevel, Role, RoleDraft } from '@harmony/administration/domain';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { RoleEditor } from './role-editor';

const OK = 200;
const ACCEPTED = 202;

const viewUsers: PermissionEntry = {
  id: 'p9',
  permissionValue: 'ViewUsers',
  name: 'View users',
  description: null,
};
const manageUsers: PermissionEntry = { ...viewUsers, id: 'p10', permissionValue: 'ManageUsers' };
const frontDesk: Role = {
  id: 'r1',
  code: 'FRONT-DESK',
  nameEn: 'Front desk',
  nameAr: 'الاستقبال',
  privilegeLevel: PrivilegeLevel.supervisor,
  isSystemRole: false,
  isSuperRole: false,
};

@Component({
  selector: 'hr-host',
  imports: [RoleEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-role-editor [role]="role()" (done)="closed.set(closed() + 1)" />`,
})
class Host {
  readonly role = signal<Role | null>(null);
  readonly closed = signal(0);
}

describe('hr-role-editor', () => {
  const created: { draft: RoleDraft; permissionIds: readonly string[]; key: IntentKey }[] = [];
  const updated: { id: string; draft: RoleDraft; key: IntentKey }[] = [];
  let answer: ApiResult<unknown>;

  const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

  const render = async (role: Role | null = null) => {
    created.length = 0;
    updated.length = 0;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: RolesStore,
          useValue: {
            permissions: signal([viewUsers, manageUsers]),
            catalogueLoading: signal(false),
            catalogueProblem: signal(null),
            loadCatalogue: () => undefined,
            create: (draft: RoleDraft, permissionIds: readonly string[], key: IntentKey) => {
              created.push({ draft, permissionIds, key });
              return Promise.resolve(answer);
            },
            update: (target: Role, draft: RoleDraft, key: IntentKey) => {
              updated.push({ id: target.id, draft, key });
              return Promise.resolve(answer);
            },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.role.set(role);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async (): Promise<void> => {
      await tick();
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const type = async (selector: string, value: string): Promise<void> => {
      const box = page.querySelector<HTMLInputElement | HTMLSelectElement>(selector);
      if (box === null) throw new Error(`no ${selector}`);
      box.value = value;
      box.dispatchEvent(new Event('input'));
      box.dispatchEvent(new Event('change'));
      await settle();
    };
    const tickBox = async (permission: string): Promise<void> => {
      page.querySelector<HTMLInputElement>(`input[data-permission="${permission}"]`)?.click();
      await settle();
    };
    const send = async (): Promise<void> => {
      page.querySelector('form')?.requestSubmit();
      await settle();
    };
    return { fixture, page, type, tickBox, send };
  };

  it('a new role: an empty submit names the missing code and name and sends nothing', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render();
    await editor.send();
    expect(created).toEqual([]);
    expect(editor.page.querySelector('#role-code-error')?.textContent).toContain('code');
    expect(editor.page.querySelector('#role-name-en-error')?.textContent).toContain('name');
  });

  it('a new role needs at least one permission; with one, it is created with the key and closes', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render();
    await editor.type('#role-code', ' night ');
    await editor.type('#role-name-en', 'Night audit ');
    await editor.type('#role-level', 'Standard');
    await editor.send();
    expect(created).toEqual([]);
    expect(editor.page.textContent).toContain('at least one permission');
    await editor.tickBox('ViewUsers');
    await editor.send();
    expect(created).toHaveLength(1);
    expect(created[0]?.draft).toEqual({
      code: 'night',
      nameEn: 'Night audit',
      nameAr: '',
      privilegeLevel: 'Standard',
    });
    expect(created[0]?.permissionIds).toEqual(['p9']);
    expect(editor.fixture.componentInstance.closed()).toBe(1);
  });

  it('a code with a space is refused in the browser', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render();
    await editor.type('#role-code', 'front desk');
    await editor.type('#role-name-en', 'Front desk');
    await editor.tickBox('ViewUsers');
    await editor.send();
    expect(created).toEqual([]);
    expect(editor.page.querySelector('#role-code-error')?.textContent).toContain('letters, digits');
  });

  it('editing: the fields start from the role, no checklist; Save updates with the key', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render(frontDesk);
    expect(editor.page.querySelector<HTMLInputElement>('#role-code')?.value).toBe('FRONT-DESK');
    expect(editor.page.querySelector<HTMLInputElement>('#role-name-ar')?.value).toBe('الاستقبال');
    expect(editor.page.querySelector<HTMLSelectElement>('#role-level')?.value).toBe('Supervisor');
    expect(editor.page.querySelector('hr-permission-picker')).toBeNull();
    await editor.type('#role-name-en', 'Reception');
    await editor.send();
    expect(updated).toHaveLength(1);
    expect(updated[0]?.id).toBe('r1');
    expect(updated[0]?.draft.nameEn).toBe('Reception');
    expect(updated[0]?.key.value).not.toBe('');
  });

  it('a refusal about the code lands under the code field; another refusal is the general line', async () => {
    answer = { kind: 'refused', status: ACCEPTED, codes: ['00016'] };
    const editor = await render(frontDesk);
    await editor.send();
    expect(editor.page.querySelector('#role-code-error')?.textContent).toContain('already used');
    // The server's error stays on the field until the value changes; a new code frees the form to submit again.
    await editor.type('#role-code', 'FRONT-DESK-2');
    answer = { kind: 'refused', status: ACCEPTED, codes: ['00077'] };
    await editor.send();
    expect(editor.page.textContent).toContain('above your own rank');
    expect(editor.fixture.componentInstance.closed()).toBe(0);
  });
});
