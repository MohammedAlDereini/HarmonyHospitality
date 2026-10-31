import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
} from '@angular/core';
import { PersonStore, RolesStore } from '@harmony/administration/data-access';
import { Role, UserAccount, roleName } from '@harmony/administration/domain';
import { AccessStore, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { IdentityConfirmation } from '@harmony/platform/session';

/** Identity refuses an account with no role at all (ModelValidationErrorCodes 10302); said here before the call. */
const NONE_CHOSEN = '10302';

/**
 * The roles one account holds: the tenant's roles as a checklist, the held ones ticked, written once as the whole
 * set with one intent key (UpdateUserRolesCommand). Without AssignUsersToRole the list is read-only.
 */
@Component({
  selector: 'hr-role-assignment',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './role-assignment.html',
  styleUrl: './role-assignment.css',
})
export class RoleAssignment {
  readonly user = input.required<UserAccount>();
  /** The roles changed; the page reads the account again. */
  readonly done = output();

  protected readonly roles = inject(RolesStore);
  private readonly person = inject(PersonStore);
  private readonly access = inject(AccessStore);
  private readonly confirmation = inject(IdentityConfirmation);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly mayAssign = computed(() => this.access.can(Permission.AssignUsersToRole));
  /** The editable copy of what the account holds; a fresh account resets it. */
  protected readonly selected = linkedSignal<ReadonlySet<string>>(
    () => new Set(this.user().roleIds),
  );
  protected readonly changed = computed(() => {
    const held = this.user().roleIds;
    const chosen = this.selected();
    return held.length !== chosen.size || held.some((id) => !chosen.has(id));
  });
  protected readonly saving = signal(false);
  protected readonly problem = signal<string | null>(null);

  constructor() {
    this.roles.load();
  }

  protected name(role: Role): string {
    return roleName(role, this.locale);
  }

  protected toggle(id: string): void {
    this.problem.set(null);
    this.selected.update((set) => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  protected save(): void {
    void this.saveNow();
  }

  private async saveNow(): Promise<void> {
    const roleIds = Array.from(this.selected());
    if (roleIds.length === 0) {
      this.problem.set(errorMessage(NONE_CHOSEN) ?? kindMessage('invalid'));
      return;
    }
    this.problem.set(null);
    this.saving.set(true);
    const result = await this.confirmation.run((intent) =>
      this.person.updateRoles(this.user(), roleIds, intent),
    );
    this.saving.set(false);
    if (result.kind === 'ok') {
      this.done.emit();
      return;
    }
    // Refused: the boxes go back to what the account holds, under the refusal's words.
    this.selected.set(new Set(this.user().roleIds));
    this.problem.set(messageOf(result));
  }
}
