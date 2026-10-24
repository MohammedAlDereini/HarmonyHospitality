import {
  ChangeDetectionStrategy,
  Component,
  afterNextRender,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { RolesStore } from '@harmony/administration/data-access';
import { Role, roleName } from '@harmony/administration/domain';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { PermissionPicker } from './permission-picker';

/** Identity refuses an empty set (ModelValidationErrorCodes 10208); said here before the call. */
const NONE_CHOSEN = '10208';

/**
 * The permissions one role holds: read from the role, changed as a set, written once with one intent key
 * (UpdateRolePermissionsCommand: the role holds exactly these afterwards).
 */
@Component({
  selector: 'hr-permissions-editor',
  imports: [PermissionPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './permissions-editor.html',
  styleUrl: './permissions-editor.css',
})
export class PermissionsEditor {
  readonly role = input.required<Role>();
  readonly done = output();
  readonly cancelled = output();

  protected readonly store = inject(RolesStore);
  private readonly locale = $localize.locale ?? 'en';
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly problem = signal<string | null>(null);

  constructor() {
    this.store.loadCatalogue();
    afterNextRender(() => {
      void this.read();
    });
  }

  protected name(): string {
    return roleName(this.role(), this.locale);
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

  protected cancel(): void {
    this.cancelled.emit();
  }

  private async read(): Promise<void> {
    const result = await this.store.detail(this.role());
    if (result.kind === 'ok') {
      this.selected.set(new Set(result.data.permissions.map((entry) => entry.id)));
    } else {
      this.problem.set(messageOf(result));
    }
    this.loading.set(false);
  }

  private async saveNow(): Promise<void> {
    const permissionIds = Array.from(this.selected());
    if (permissionIds.length === 0) {
      this.problem.set(errorMessage(NONE_CHOSEN) ?? kindMessage('invalid'));
      return;
    }
    this.problem.set(null);
    this.saving.set(true);
    const result = await this.store.setPermissions(this.role(), permissionIds, newIntentKey());
    this.saving.set(false);
    if (result.kind === 'ok') {
      this.done.emit();
      return;
    }
    this.problem.set(messageOf(result));
  }
}
