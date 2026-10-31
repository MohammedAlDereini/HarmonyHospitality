import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { RolesStore, UsersStore } from '@harmony/administration/data-access';
import { PermissionEntry, Role, UserAccount, roleName } from '@harmony/administration/domain';
import { AccessStore, IfAllowed, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { initialsOf } from '@harmony/shared/domain';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead, PopAt } from '@harmony/ui/shell';
import { permissionLabel, privilegeLabel, RoleEditor } from '@harmony/administration/feature-roles';

/** The areas the permissions fall into, in the order the matrix shows them. */
type Area = 'tenants' | 'properties' | 'lookups' | 'people' | 'roles' | 'settings' | 'other';
const areas: readonly Area[] = [
  'tenants',
  'properties',
  'lookups',
  'people',
  'roles',
  'settings',
  'other',
];
const areaOf: Readonly<Record<string, Area>> = {
  ViewTenants: 'tenants',
  ManageTenants: 'tenants',
  ViewProperties: 'properties',
  ManageProperties: 'properties',
  ViewLookups: 'lookups',
  ManageLookups: 'lookups',
  ViewUsers: 'people',
  ManageUsers: 'people',
  ViewRoles: 'roles',
  CreateRole: 'roles',
  EditRole: 'roles',
  DeleteRole: 'roles',
  ManageRolePermissions: 'roles',
  AssignUsersToRole: 'roles',
  ViewSettings: 'settings',
  ManageSettings: 'settings',
};
const areaTitle: Readonly<Record<Area, string>> = {
  tenants: $localize`:@@administration.console.roles.area.tenants:Tenants · platform only`,
  properties: $localize`:@@administration.console.roles.area.properties:Properties`,
  lookups: $localize`:@@administration.console.roles.area.lookups:Lookups (reference data)`,
  people: $localize`:@@administration.console.roles.area.people:People`,
  roles: $localize`:@@administration.console.roles.area.roles:Roles`,
  settings: $localize`:@@administration.console.roles.area.settings:Settings`,
  other: $localize`:@@administration.console.roles.area.other:Other`,
};
/** System roles lead the matrix (locked); the custom roles follow by name. */
const systemFirst = (a: Role, b: Role): number => {
  const bySystem = Number(b.isSystemRole) - Number(a.isSystemRole);
  return bySystem === 0 ? a.nameEn.localeCompare(b.nameEn) : bySystem;
};

/** How many people the role card names before "more". */
const PEOPLE_SHOWN = 5;

/** One block of the matrix: an area and its permissions. */
interface AreaRows {
  readonly area: Area;
  readonly title: string;
  readonly entries: readonly PermissionEntry[];
}

/** What one of the drawers holds: a new role or a role being changed. */
type Open = { readonly kind: 'create' } | { readonly kind: 'edit'; readonly role: Role };

/**
 * The platform's roles as a permission matrix (platform console design, C7): one column per role, one row per
 * permission grouped by area; a system role is read only, a custom role's cells are ticked here and saved together.
 * The card beside the matrix names the people in the chosen role. New, edit and delete are the same doors the role
 * list had. Every save carries its own intent key; the roles are read again after it.
 */
@Component({
  selector: 'hr-roles-console-page',
  imports: [RouterLink, IfAllowed, Icon, PageHead, PopAt, Drawer, ConfirmDialog, RoleEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './roles-console-page.html',
  styleUrl: './roles-console-page.css',
})
export class RolesConsolePage {
  protected readonly store = inject(RolesStore);
  private readonly users = inject(UsersStore);
  private readonly access = inject(AccessStore);
  private readonly toasts = inject(Toasts);
  private readonly editorDrawer = viewChild.required<Drawer>('editorDrawer');
  private readonly confirm = viewChild.required(ConfirmDialog);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly createRole = Permission.CreateRole;
  protected readonly editRole = Permission.EditRole;
  protected readonly deleteRole = Permission.DeleteRole;
  protected readonly mayGrant = computed(() => this.access.can(Permission.ManageRolePermissions));
  protected readonly drawer = signal<Open | null>(null);
  protected readonly removing = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly actionProblem = signal<string | null>(null);
  /** The role whose people the side card names; the first custom role until one is chosen. */
  protected readonly chosenId = signal<string | null>(null);
  /** What Identity says each role holds, by role id; read once per role. */
  private readonly granted = signal<ReadonlyMap<string, ReadonlySet<string>>>(new Map());
  /** What the person has ticked and not saved yet, by role id. */
  private readonly draft = signal<ReadonlyMap<string, ReadonlySet<string>>>(new Map());

  protected readonly blocks = computed<readonly AreaRows[]>(() => {
    const entries = this.store.permissions();
    return areas
      .map((area) => ({
        area,
        title: areaTitle[area],
        entries: entries.filter((entry) => (areaOf[entry.permissionValue] ?? 'other') === area),
      }))
      .filter((block) => block.entries.length > 0);
  });
  protected readonly people = computed(() => this.users.everyone());
  /** The matrix columns, as the design orders them. */
  protected readonly columns = computed(() => [...this.store.roles()].sort(systemFirst));
  protected readonly chosen = computed<Role | null>(() => {
    const roles = this.store.roles();
    const id = this.chosenId();
    return (
      roles.find((role) => role.id === id) ??
      roles.find((role) => !role.isSystemRole) ??
      roles[0] ??
      null
    );
  });
  protected readonly chosenPeople = computed<readonly UserAccount[]>(() => {
    const role = this.chosen();
    return role === null ? [] : this.people().filter((person) => person.roleIds.includes(role.id));
  });
  protected readonly shownPeople = computed(() => this.chosenPeople().slice(0, PEOPLE_SHOWN));
  protected readonly morePeople = computed(() =>
    Math.max(0, this.chosenPeople().length - PEOPLE_SHOWN),
  );
  /** Every (role, permission) pair ticked differently from what Identity holds. */
  protected readonly changes = computed(() => {
    let count = 0;
    this.draft().forEach((ticked, roleId) => {
      const held = this.granted().get(roleId) ?? new Set<string>();
      ticked.forEach((id) => {
        if (!held.has(id)) count += 1;
      });
      held.forEach((id) => {
        if (!ticked.has(id)) count += 1;
      });
    });
    return count;
  });
  /** "Support gains ViewUsers; Billing loses ViewProperties": what Save changes will do. */
  protected readonly changeSummary = computed(() => {
    const parts: string[] = [];
    this.draft().forEach((ticked, roleId) => {
      const role = this.store.roles().find((candidate) => candidate.id === roleId);
      if (role === undefined) return;
      const held = this.granted().get(roleId) ?? new Set<string>();
      const gains = this.names([...ticked].filter((id) => !held.has(id)));
      const loses = this.names([...held].filter((id) => !ticked.has(id)));
      if (gains !== '')
        parts.push(
          $localize`:@@administration.console.roles.gains:${roleName(role, this.locale)}:ROLE: gains ${gains}:PERMISSIONS:`,
        );
      if (loses !== '')
        parts.push(
          $localize`:@@administration.console.roles.loses:${roleName(role, this.locale)}:ROLE: loses ${loses}:PERMISSIONS:`,
        );
    });
    return parts.join('; ');
  });
  protected readonly editorHeading = computed(() =>
    this.drawer()?.kind === 'edit'
      ? $localize`:@@administration.roles.drawer.edit:Edit role`
      : $localize`:@@administration.roles.drawer.new:New role`,
  );

  constructor() {
    this.store.load();
    this.store.loadCatalogue();
    this.users.countAll();
    effect(() => {
      const roles = this.store.roles();
      untracked(() => {
        void this.readGrants(roles);
      });
    });
  }

  protected name(role: Role): string {
    return roleName(role, this.locale);
  }

  protected level(role: Role): string {
    return privilegeLabel(role.privilegeLevel);
  }

  /** How many people hold the role. */
  protected peopleIn(role: Role): number {
    return this.people().filter((person) => person.roleIds.includes(role.id)).length;
  }

  protected label(entry: PermissionEntry): string {
    return permissionLabel(entry.permissionValue, entry.name);
  }

  /** Whether the role holds the permission as ticked now (the draft first, then what Identity holds). */
  protected has(role: Role, entry: PermissionEntry): boolean {
    if (role.isSuperRole) return true;
    const ticked = this.draft().get(role.id) ?? this.granted().get(role.id);
    return ticked?.has(entry.id) ?? false;
  }

  protected toggle(role: Role, entry: PermissionEntry): void {
    const current = new Set(this.draft().get(role.id) ?? this.granted().get(role.id) ?? []);
    if (current.has(entry.id)) current.delete(entry.id);
    else current.add(entry.id);
    this.draft.update((draft) => new Map(draft).set(role.id, current));
  }

  protected choose(role: Role): void {
    this.chosenId.set(role.id);
  }

  protected initials(person: UserAccount): string {
    return initialsOf(person.displayName);
  }

  protected personNote(person: UserAccount): string {
    return this.store
      .roles()
      .filter((role) => person.roleIds.includes(role.id))
      .map((role) => roleName(role, this.locale))
      .join(' · ');
  }

  protected discard(): void {
    this.draft.set(new Map());
  }

  protected save(): void {
    void this.saveNow();
  }

  protected openCreator(): void {
    this.actionProblem.set(null);
    this.drawer.set({ kind: 'create' });
    this.editorDrawer().open();
  }

  protected openEditor(role: Role): void {
    this.actionProblem.set(null);
    this.drawer.set({ kind: 'edit', role });
    this.editorDrawer().open();
  }

  protected saved(): void {
    this.toasts.show(
      this.drawer()?.kind === 'edit'
        ? $localize`:@@administration.roles.toast.saved:Role saved.`
        : $localize`:@@administration.roles.toast.added:Role added.`,
    );
    this.editorDrawer().close();
  }

  protected askDelete(role: Role): void {
    void this.askDeleteNow(role);
  }

  private names(ids: readonly string[]): string {
    return this.store
      .permissions()
      .filter((entry) => ids.includes(entry.id))
      .map((entry) => entry.permissionValue)
      .join(', ');
  }

  private async readGrants(roles: readonly Role[]): Promise<void> {
    const read = await Promise.all(
      roles
        .filter((role) => !role.isSuperRole)
        .map(async (role) => {
          const result = await this.store.detail(role);
          return result.kind === 'ok'
            ? ([role.id, new Set(result.data.permissions.map((entry) => entry.id))] as const)
            : null;
        }),
    );
    const granted = new Map<string, ReadonlySet<string>>();
    read.forEach((pair) => {
      if (pair !== null) granted.set(pair[0], pair[1]);
    });
    this.granted.set(granted);
    this.draft.set(new Map());
  }

  /** Every role with a change is saved on its own press key, together; the roles are read again after. */
  private async saveNow(): Promise<void> {
    this.actionProblem.set(null);
    this.saving.set(true);
    const roles = this.store.roles();
    const outcomes = await Promise.all(
      [...this.draft()].map(async ([roleId, ticked]) => {
        const role = roles.find((candidate) => candidate.id === roleId);
        if (role === undefined) return null;
        const result = await this.store.setPermissions(role, [...ticked], newIntentKey());
        return result.kind === 'ok' ? null : `${roleName(role, this.locale)}: ${messageOf(result)}`;
      }),
    );
    this.saving.set(false);
    const problems = outcomes.filter((outcome): outcome is string => outcome !== null);
    if (problems.length > 0) {
      this.actionProblem.set(problems.join(' · '));
    } else {
      this.toasts.show($localize`:@@administration.roles.toast.permissions:Permissions saved.`);
    }
    await this.readGrants(roles);
  }

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
