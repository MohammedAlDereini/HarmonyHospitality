import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RolesStore } from '@harmony/administration/data-access';
import { PermissionEntry, PrivilegeLevel, Role, RoleDetail } from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { PageActions } from '@harmony/ui/shell';
import { RolesPage } from './roles-page';

const OK = 200;
const ACCEPTED = 202;

const frontDesk: Role = {
  id: 'r1',
  code: 'FRONT-DESK',
  nameEn: 'Front desk',
  nameAr: 'الاستقبال',
  privilegeLevel: PrivilegeLevel.standard,
  isSystemRole: false,
  isSuperRole: false,
};
const administrator: Role = {
  ...frontDesk,
  id: 'r0',
  code: 'ADMIN',
  nameEn: 'Administrator',
  nameAr: null,
  privilegeLevel: PrivilegeLevel.administrator,
  isSystemRole: true,
};
const viewUsers: PermissionEntry = {
  id: 'p9',
  permissionValue: 'ViewUsers',
  name: 'View users',
  description: null,
};

class FakeStore {
  readonly roles = signal<readonly Role[]>([]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  readonly permissions = signal<readonly PermissionEntry[]>([viewUsers]);
  readonly catalogueLoading = signal(false);
  readonly catalogueProblem = signal<string | null>(null);
  loads = 0;
  removed: { id: string; key: IntentKey }[] = [];
  removeAnswer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  load(): void {
    this.loads += 1;
  }
  loadCatalogue(): void {
    return undefined;
  }
  detail(role: Role): Promise<ApiResult<RoleDetail>> {
    return Promise.resolve({ kind: 'ok', status: OK, data: { ...role, permissions: [viewUsers] } });
  }
  remove(role: Role, key: IntentKey): Promise<ApiResult<unknown>> {
    this.removed.push({ id: role.id, key });
    return Promise.resolve(this.removeAnswer);
  }
}

describe('RolesPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<RolesPage>;
  let page: HTMLElement;
  let mayManage = true;

  const render = async (): Promise<void> => {
    fixture.detectChanges();
    await fixture.whenStable();
  };
  /** A write settles on a later microtask than whenStable waits for. */
  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    await render();
  };
  const text = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) => cell.textContent.trim());
  const click = (selector: string): void => {
    page.querySelector<HTMLElement>(selector)?.click();
  };

  beforeEach(async () => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        { provide: RolesStore, useValue: store },
        { provide: AccessStore, useValue: { can: () => mayManage } },
      ],
    });
    fixture = TestBed.createComponent(RolesPage);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  it('asks for the roles on arrival and lists code, both names, level and kind; a system role offers nothing', async () => {
    expect(store.loads).toBe(1);
    store.roles.set([frontDesk, administrator]);
    await render();
    const first = '[role="listitem"]:first-child';
    const last = '[role="listitem"]:last-child';
    expect(text(`${first} .code`)).toEqual(['FRONT-DESK']);
    expect(text(`${first} .s-nm b, ${first} .s-nm span`)).toEqual(['Front desk', 'الاستقبال']);
    expect(text(`${first} .obj b`)).toEqual(['Standard', 'Custom']);
    expect(text(`${first} .act`)).toEqual(['Edit', 'Permissions', 'Delete']);
    expect(text(`${last} .obj b`)).toEqual(['Administrator', 'System']);
    expect(text(`${last} .act`)).toEqual([]);
  });

  it('the page offers Add role to the context bar; pressing it opens the editor above the rows', async () => {
    store.roles.set([frontDesk]);
    await render();
    const offered = TestBed.inject(PageActions).template();
    if (offered === null) throw new Error('nothing offered to the slot');
    const slot = document.createElement('div');
    const view = offered.createEmbeddedView(undefined);
    view.detectChanges();
    view.rootNodes.forEach((node: Node) => slot.appendChild(node));
    const button = slot.querySelector<HTMLButtonElement>('.pbtn');
    expect(button?.textContent.trim()).toBe('Add role');
    button?.click();
    await render();
    expect(page.querySelector('hr-role-editor')).not.toBeNull();
    view.destroy();
  });

  it('Edit and Permissions open their editors under the row', async () => {
    store.roles.set([frontDesk]);
    await render();
    click('[data-act="edit"]');
    await render();
    expect(page.querySelector('hr-role-editor')).not.toBeNull();
    click('[data-act="permissions"]');
    await render();
    expect(page.querySelector('hr-role-editor')).toBeNull();
    expect(page.querySelector('hr-permissions-editor')).not.toBeNull();
  });

  it('Delete asks first; confirming removes with a fresh key; a refusal is worded and the row stays', async () => {
    store.roles.set([frontDesk]);
    await render();
    click('[data-act="delete"]');
    await render();
    expect(page.querySelector('.confirm .warning')?.textContent).toContain('cannot be undone');
    expect(store.removed).toEqual([]);
    click('[data-act="delete-confirm"]');
    await settle();
    expect(store.removed.map((call) => call.id)).toEqual(['r1']);
    expect(store.removed[0]?.key.value).not.toBe('');
    expect(page.querySelector('.confirm')).toBeNull();
    store.removeAnswer = { kind: 'refused', status: ACCEPTED, codes: ['00013'] };
    click('[data-act="delete"]');
    await render();
    click('[data-act="delete-confirm"]');
    await settle();
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('cannot be deleted');
  });

  it('without the permissions: no action buttons at all', async () => {
    mayManage = false;
    store.roles.set([frontDesk]);
    await render();
    expect(page.querySelector('.act')).toBeNull();
    mayManage = true;
  });
});
