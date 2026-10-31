import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RolesStore } from '@harmony/administration/data-access';
import { Role, roleName } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, Drawer, Toasts } from '@harmony/ui/overlays';
import { PageActionsTemplate, PageHead } from '@harmony/ui/shell';
import { PermissionsEditor } from './permissions-editor';
import { RoleEditor } from './role-editor';
import { privilegeLabel } from './wording';

/** What one of the drawers holds: a new role, a role being changed, or a role's permission set. */
type Open =
  | { readonly kind: 'create' }
  | { readonly kind: 'edit'; readonly role: Role }
  | { readonly kind: 'permissions'; readonly role: Role };

/** The name in the other language, shown under the main one so both are always visible. */
interface OtherName {
  readonly text: string;
  readonly lang: 'en' | 'ar';
}

/**
 * The customer's roles the way the platform app listed things: a lead, a real table; and, with the permissions,
 * the writes in drawers: a new role from the context bar's action slot, edit, the permission set; delete after a
 * confirmation (architecture 6, administration). System roles are shown but never changed. The buttons are
 * presentation: the API refuses on its own.
 */
@Component({
  selector: 'hr-roles-page',
  imports: [
    IfAllowed,
    Icon,
    PageActionsTemplate,
    RoleEditor,
    PermissionsEditor,
    Drawer,
    ConfirmDialog,
    PageHead,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './roles-page.html',
  styleUrl: './roles-page.css',
})
export class RolesPage {
  protected readonly store = inject(RolesStore);
  private readonly toasts = inject(Toasts);
  private readonly editorDrawer = viewChild.required<Drawer>('editorDrawer');
  private readonly permissionsDrawer = viewChild.required<Drawer>('permissionsDrawer');
  private readonly confirm = viewChild.required(ConfirmDialog);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly createRole = Permission.CreateRole;
  protected readonly editRole = Permission.EditRole;
  protected readonly deleteRole = Permission.DeleteRole;
  protected readonly managePermissions = Permission.ManageRolePermissions;

  protected readonly drawer = signal<Open | null>(null);
  /** The role being deleted right now, if any. */
  protected readonly removing = signal<string | null>(null);
  /** One sentence when a deletion was refused; null while all is well. */
  protected readonly actionProblem = signal<string | null>(null);
  protected readonly editorHeading = computed(() =>
    this.drawer()?.kind === 'edit'
      ? $localize`:@@administration.roles.edit.title:Edit role`
      : $localize`:@@administration.roles.new.title:New role`,
  );
  protected readonly permissionsHeading = computed(() => {
    const open = this.drawer();
    return open?.kind === 'permissions'
      ? $localize`:@@administration.roles.permissions.title:Permissions of ${this.name(open.role)}:ROLE:`
      : '';
  });

  constructor() {
    this.store.load();
  }

  protected name(role: Role): string {
    return roleName(role, this.locale);
  }

  protected other(role: Role): OtherName | null {
    if (this.locale.startsWith('ar')) {
      return role.nameAr === null || role.nameAr === '' ? null : { text: role.nameEn, lang: 'en' };
    }
    return role.nameAr === null || role.nameAr === '' ? null : { text: role.nameAr, lang: 'ar' };
  }

  protected level(role: Role): string {
    return privilegeLabel(role.privilegeLevel);
  }

  /** System roles ship with the platform: Identity refuses every change (00012, 00013). */
  protected canChange(role: Role): boolean {
    return !role.isSystemRole;
  }

  protected opened(role: Role): 'edit' | 'permissions' | null {
    const open = this.drawer();
    if (open === null || open.kind === 'create' || open.role.id !== role.id) return null;
    return open.kind;
  }

  protected openCreator(): void {
    this.actionProblem.set(null);
    this.permissionsDrawer().close();
    this.drawer.set({ kind: 'create' });
    this.editorDrawer().open();
  }

  protected openEditor(role: Role): void {
    this.actionProblem.set(null);
    this.permissionsDrawer().close();
    this.drawer.set({ kind: 'edit', role });
    this.editorDrawer().open();
  }

  protected openPermissions(role: Role): void {
    this.actionProblem.set(null);
    this.editorDrawer().close();
    this.drawer.set({ kind: 'permissions', role });
    this.permissionsDrawer().open();
  }

  /** The editor drawer closed: its content goes, unless another drawer has already taken over. */
  protected editorClosed(): void {
    const open = this.drawer();
    if (open !== null && open.kind !== 'permissions') this.drawer.set(null);
  }

  protected permissionsClosed(): void {
    if (this.drawer()?.kind === 'permissions') this.drawer.set(null);
  }

  protected saved(): void {
    this.toasts.show(
      this.drawer()?.kind === 'edit'
        ? $localize`:@@administration.roles.toast.saved:Role saved.`
        : $localize`:@@administration.roles.toast.added:Role added.`,
    );
    this.editorDrawer().close();
  }

  protected permissionsSaved(): void {
    this.toasts.show($localize`:@@administration.roles.toast.permissions:Permissions saved.`);
    this.permissionsDrawer().close();
  }

  protected askDelete(role: Role): void {
    void this.askDeleteNow(role);
  }

  /** The confirm dialog first; then one intent key per press; a refusal is worded once and the row stays. */
  private async askDeleteNow(role: Role): Promise<void> {
    if (!(await this.confirm().ask())) return;
    this.actionProblem.set(null);
    this.removing.set(role.id);
    const result = await this.store.remove(role, newIntentKey());
    this.removing.set(null);
    if (result.kind === 'ok') {
      this.toasts.show($localize`:@@administration.roles.toast.deleted:Role deleted.`);
      return;
    }
    this.actionProblem.set(messageOf(result));
  }
}
