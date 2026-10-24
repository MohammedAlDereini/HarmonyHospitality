import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RolesStore } from '@harmony/administration/data-access';
import { PermissionEntry, PrivilegeLevel, Role, RoleDetail } from '@harmony/administration/domain';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { PermissionsEditor } from './permissions-editor';

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
  nameAr: null,
  privilegeLevel: PrivilegeLevel.standard,
  isSystemRole: false,
  isSuperRole: false,
};

@Component({
  selector: 'hr-host',
  imports: [PermissionsEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-permissions-editor [role]="role" (done)="closed.set(closed() + 1)" />`,
})
class Host {
  readonly role = frontDesk;
  readonly closed = signal(0);
}

describe('hr-permissions-editor', () => {
  const saved: { id: string; permissionIds: readonly string[]; key: IntentKey }[] = [];
  let answer: ApiResult<unknown>;

  const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

  const render = async () => {
    saved.length = 0;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: RolesStore,
          useValue: {
            permissions: signal([viewUsers, manageUsers]),
            catalogueLoading: signal(false),
            catalogueProblem: signal(null),
            loadCatalogue: () => undefined,
            detail: (role: Role): Promise<ApiResult<RoleDetail>> =>
              Promise.resolve({
                kind: 'ok',
                status: OK,
                data: { ...role, permissions: [viewUsers] },
              }),
            setPermissions: (role: Role, permissionIds: readonly string[], key: IntentKey) => {
              saved.push({ id: role.id, permissionIds, key });
              return Promise.resolve(answer);
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
    await settle();
    const box = (permission: string): HTMLInputElement | null =>
      page.querySelector<HTMLInputElement>(`input[data-permission="${permission}"]`);
    const save = async (): Promise<void> => {
      page.querySelector<HTMLButtonElement>('[data-act="save-permissions"]')?.click();
      await settle();
    };
    return { fixture, page, box, save, settle };
  };

  it("the role's own permissions arrive ticked; a change is saved as the whole set with a key", async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render();
    expect(editor.page.querySelector('h2')?.textContent).toContain('Front desk');
    expect(editor.box('ViewUsers')?.checked).toBe(true);
    expect(editor.box('ManageUsers')?.checked).toBe(false);
    editor.box('ManageUsers')?.click();
    await editor.settle();
    await editor.save();
    expect(saved).toHaveLength(1);
    expect(saved[0]?.id).toBe('r1');
    expect([...(saved[0]?.permissionIds ?? [])].sort()).toEqual(['p10', 'p9']);
    expect(saved[0]?.key.value).not.toBe('');
    expect(editor.fixture.componentInstance.closed()).toBe(1);
  });

  it('clearing every box is refused in the browser; a server refusal is worded and the editor stays', async () => {
    answer = { kind: 'refused', status: ACCEPTED, codes: ['00012'] };
    const editor = await render();
    editor.box('ViewUsers')?.click();
    await editor.settle();
    await editor.save();
    expect(saved).toEqual([]);
    expect(editor.page.textContent).toContain('at least one permission');
    editor.box('ViewUsers')?.click();
    await editor.settle();
    await editor.save();
    expect(saved).toHaveLength(1);
    expect(editor.page.textContent).toContain('cannot be changed');
    expect(editor.fixture.componentInstance.closed()).toBe(0);
  });
});
