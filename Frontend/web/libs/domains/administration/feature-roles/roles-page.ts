import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RolesStore } from '@harmony/administration/data-access';
import { Role, roleName } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { Icon } from '@harmony/ui/icons';
import { PageActionsTemplate } from '@harmony/ui/shell';
import { PermissionsEditor } from './permissions-editor';
import { RoleEditor } from './role-editor';
import { privilegeLabel } from './wording';

/** What is open under a row. */
type Opened = 'edit' | 'permissions' | 'delete';

interface Editing {
  readonly id: string;
  readonly kind: Opened;
}

/** The name in the other language, shown under the main one so both are always visible. */
interface OtherName {
  readonly text: string;
  readonly lang: 'en' | 'ar';
}

/**
 * The customer's roles: code, names, privilege level, kind; and, with the permissions, the writes: a new role from
 * the context bar's action slot, edit, the permission set, delete (architecture 6, administration). System roles
 * are shown but never changed. The buttons are presentation: the API refuses on its own.
 */
@Component({
  selector: 'hr-roles-page',
  imports: [IfAllowed, Icon, PageActionsTemplate, RoleEditor, PermissionsEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './roles-page.html',
  styleUrl: './roles-page.css',
})
export class RolesPage {
  protected readonly store = inject(RolesStore);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly createRole = Permission.CreateRole;
  protected readonly editRole = Permission.EditRole;
  protected readonly deleteRole = Permission.DeleteRole;
  protected readonly managePermissions = Permission.ManageRolePermissions;

  protected readonly creating = signal(false);
  protected readonly editing = signal<Editing | null>(null);
  /** The role being deleted right now, if any. */
  protected readonly removing = signal<string | null>(null);
  /** One sentence when a deletion was refused; null while all is well. */
  protected readonly actionProblem = signal<string | null>(null);

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

  protected opened(role: Role): Opened | null {
    const editing = this.editing();
    return editing !== null && editing.id === role.id ? editing.kind : null;
  }

  protected openCreator(): void {
    this.actionProblem.set(null);
    this.creating.set(true);
  }

  protected closeCreator(): void {
    this.creating.set(false);
  }

  protected open(role: Role, kind: Opened): void {
    this.actionProblem.set(null);
    this.editing.set({ id: role.id, kind });
  }

  protected close(): void {
    this.editing.set(null);
  }

  protected remove(role: Role): void {
    void this.removeNow(role);
  }

  /** One intent key per press; the store reads the list again on success; a refusal is worded once. */
  private async removeNow(role: Role): Promise<void> {
    this.actionProblem.set(null);
    this.removing.set(role.id);
    const result = await this.store.remove(role, newIntentKey());
    this.removing.set(null);
    if (result.kind === 'ok') {
      this.close();
      return;
    }
    this.actionProblem.set(messageOf(result));
  }
}
