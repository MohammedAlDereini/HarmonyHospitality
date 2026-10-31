import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import { FormField, form, submit } from '@angular/forms/signals';
import { DirectoryStore, RolesStore } from '@harmony/administration/data-access';
import { DirectoryGroup, groupName, roleName } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { MyProperties, propertyName } from '@harmony/platform/context';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { IdentityConfirmation } from '@harmony/platform/session';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, Drawer, Toasts } from '@harmony/ui/overlays';
import {
  scopeOf,
  sentForApproval,
  sentForApprovalToast,
} from '@harmony/administration/feature-users';
import { PeopleHead } from './people-head';
import { stampUtc } from './stamps';

/** A choice in the Where list: empty for every hotel, h:<hotel id> or g:<group id> (the access editor's spelling). */
const hotelPrefix = 'h:';
const groupPrefix = 'g:';
/** A directory's external id is shown cut, like a fingerprint. */
const EXTERNAL_ID_SHOWN = 4;

const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

interface MapDraft {
  readonly roleId: string;
  readonly where: string;
}

const whereOf = (group: DirectoryGroup): string => {
  if (group.propertyId !== null) return hotelPrefix + group.propertyId;
  if (group.propertyGroupId !== null) return groupPrefix + group.propertyGroupId;
  return '';
};

/**
 * The platform's own company directory (platform console design, C22, a tab of People): the connection with its
 * addresses, client id and secret age; the one-time secret as a message bar with its copies; the directory groups
 * with the role each gives and where, changed in the drawer. Making a new secret is asked first. One intent key per
 * press.
 */
@Component({
  selector: 'hr-directory-console-page',
  imports: [FormField, IfAllowed, Icon, ConfirmDialog, Drawer, PeopleHead],
  providers: [DirectoryStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './directory-console-page.html',
  styleUrl: './directory-console-page.css',
})
export class DirectoryConsolePage {
  protected readonly store = inject(DirectoryStore);
  protected readonly roles = inject(RolesStore);
  protected readonly hotels = inject(MyProperties);
  private readonly toasts = inject(Toasts);
  private readonly confirmation = inject(IdentityConfirmation);
  private readonly secretDialog = viewChild.required<ConfirmDialog>('secretDialog');
  private readonly mapDrawer = viewChild.required<Drawer>('mapDrawer');
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly assignRoles = Permission.AssignUsersToRole;
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);
  /** The group whose role and place are being changed in the drawer. */
  protected readonly mapping = signal<DirectoryGroup | null>(null);
  private readonly model = signal<MapDraft>({ roleId: '', where: '' });
  protected readonly form = form(this.model);
  protected readonly mapProblem = signal<string | null>(null);

  constructor() {
    this.loadNames();
  }

  protected stamp(instant: string): string {
    return stampUtc(instant);
  }

  /** "Entra ID · 4a7c…": where the group comes from, by the start of its id. */
  protected sourceOf(group: DirectoryGroup): string {
    if (group.externalId === null) return '';
    return `${group.externalId.slice(0, EXTERNAL_ID_SHOWN)}…`;
  }

  protected roleOf(group: DirectoryGroup): string | null {
    if (group.roleId === null) return null;
    const role = this.roles.roles().find((r) => r.id === group.roleId);
    return role === undefined ? '…' : roleName(role, this.locale);
  }

  protected whereOf(group: DirectoryGroup): string {
    if (group.roleId === null) return '—';
    if (group.propertyId !== null) {
      const hotel = this.hotels.list().find((h) => h.id === group.propertyId);
      return hotel === undefined ? '…' : propertyName(hotel, this.locale);
    }
    if (group.propertyGroupId !== null) {
      const choice = this.store.groupChoices().find((g) => g.id === group.propertyGroupId);
      return choice === undefined ? '…' : groupName(choice, this.locale);
    }
    return $localize`:@@administration.directory.every-hotel:Every hotel`;
  }

  protected hotelChoice(id: string): string {
    return hotelPrefix + id;
  }

  protected groupChoice(id: string): string {
    return groupPrefix + id;
  }

  protected hotelLabel(id: string): string {
    const hotel = this.hotels.list().find((h) => h.id === id);
    return hotel === undefined ? id : propertyName(hotel, this.locale);
  }

  protected roleLabel(id: string): string {
    const role = this.roles.roles().find((r) => r.id === id);
    return role === undefined ? id : roleName(role, this.locale);
  }

  protected choiceLabel(id: string): string {
    const choice = this.store.groupChoices().find((g) => g.id === id);
    return choice === undefined ? id : groupName(choice, this.locale);
  }

  /** The names on the page need the roles, the hotels and the hotel groups. */
  protected loadNames(): void {
    this.roles.load();
    void this.hotels.ready();
    this.store.loadChoices();
  }

  protected async open(): Promise<void> {
    const result = await this.run(this.store.open(newIntentKey()));
    if (result.kind === 'ok')
      this.toasts.show(
        $localize`:@@administration.directory.toast.opened:The connection is open. Copy the secret now.`,
      );
    else this.problem.set(messageOf(result));
  }

  /** A new secret is asked first: the directory stops until it gets the new one. */
  protected async newSecret(): Promise<void> {
    if (!(await this.secretDialog().ask())) return;
    const result = await this.run(this.store.newSecret(newIntentKey()));
    if (result.kind === 'ok')
      this.toasts.show(
        $localize`:@@administration.directory.toast.new-secret:A new secret is made. Copy it now.`,
      );
    else this.problem.set(messageOf(result));
  }

  protected async switchTo(on: boolean): Promise<void> {
    const result = await this.run(this.store.switch(on, newIntentKey()));
    if (result.kind === 'ok')
      this.toasts.show(
        on
          ? $localize`:@@administration.directory.toast.on:The company directory is on.`
          : $localize`:@@administration.directory.toast.off:The company directory is off.`,
      );
    else this.problem.set(messageOf(result));
  }

  protected async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.toasts.show($localize`:@@administration.directory.toast.copied:Copied.`);
    } catch {
      this.problem.set(
        $localize`:@@administration.directory.copy-failed:The browser did not allow copying. Select the text and copy it.`,
      );
    }
  }

  protected startMapping(group: DirectoryGroup): void {
    this.loadNames();
    this.problem.set(null);
    this.mapProblem.set(null);
    this.model.set({ roleId: group.roleId ?? '', where: whereOf(group) });
    this.mapping.set(group);
    this.mapDrawer().open();
  }

  protected async saveMapping(event: Event, group: DirectoryGroup): Promise<void> {
    event.preventDefault();
    await submit(this.form, async () => {
      const draft = this.model();
      const roleId = draft.roleId === '' ? null : draft.roleId;
      const scope = draft.where === '' ? null : scopeOf(draft.where);
      if (roleId === null && scope !== null) {
        this.mapProblem.set(wording('11103'));
        return;
      }
      const result = await this.run(
        this.confirmation.run((intent) => this.store.mapGroup(group, roleId, scope, intent)),
      );
      if (result.kind === 'ok') {
        this.mapDrawer().close();
        this.toasts.show(
          $localize`:@@administration.directory.toast.mapped:Saved. Its members' access follows now.`,
        );
      } else if (sentForApproval(result)) {
        this.mapDrawer().close();
        this.toasts.show(sentForApprovalToast());
      } else {
        this.mapProblem.set(messageOf(result));
      }
    });
  }

  /** One press: nothing else until it settles. */
  private async run(call: Promise<ApiResult<unknown>>): Promise<ApiResult<unknown>> {
    this.problem.set(null);
    this.busy.set(true);
    const result = await call;
    this.busy.set(false);
    return result;
  }
}
