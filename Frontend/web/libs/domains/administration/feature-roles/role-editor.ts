import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, maxLength, pattern, required, submit } from '@angular/forms/signals';
import { RolesStore } from '@harmony/administration/data-access';
import { PrivilegeLevel, Role, RoleDraft } from '@harmony/administration/domain';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { PermissionPicker } from './permission-picker';
import { privilegeLabel } from './wording';

/** Identity's limits (CreateRoleCommand / EditRoleCommand): the code 32, each name 128 characters. */
const CODE_MAX_LENGTH = 32;
const NAME_MAX_LENGTH = 128;
// A role code as Identity accepts it (IdentityText.RoleCode): 2 to 32 ASCII letters, digits or dashes, with the
// surrounding spaces the form trims before sending; the server upper-cases it. Matches " night-audit "; rejects "a"
// (too short) and "front desk" (a space inside).
// Tester: https://regex101.com/?regex=%5E%5Cs*%5BA-Za-z0-9-%5D%7B2%2C32%7D%5Cs*%24&testString=%20night-audit%20
const roleCode = /^\s*[A-Za-z0-9-]{2,32}\s*$/;
/** The field each of Identity's codes belongs to, whatever the kind of refusal; others become the general line. */
const fieldOfCode: Readonly<Record<string, keyof RoleDraft>> = {
  '10202': 'code',
  '10203': 'code',
  '00006': 'code',
  '00016': 'code',
  '00017': 'code',
  '10204': 'nameEn',
  '10205': 'nameEn',
  '10206': 'nameAr',
  '10207': 'privilegeLevel',
  '00014': 'privilegeLevel',
};
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');
const empty: RoleDraft = {
  code: '',
  nameEn: '',
  nameAr: '',
  privilegeLevel: PrivilegeLevel.standard,
};

/**
 * A role's code, names and level; for a new role also the permissions it starts with (Identity wants at least one).
 * A Signal Form over an editable copy of the row (architecture 7.9, linkedSignal), one intent key per press, the
 * server's codes back under their fields, everything else worded once.
 */
@Component({
  selector: 'hr-role-editor',
  imports: [FormField, PermissionPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './role-editor.html',
  styleUrl: './role-editor.css',
})
export class RoleEditor {
  /** The role being edited, or null for a new one. */
  readonly role = input<Role | null>(null);
  readonly done = output();
  readonly cancelled = output();

  protected readonly store = inject(RolesStore);
  private readonly codeBox = viewChild.required<ElementRef<HTMLInputElement>>('codeBox');
  private readonly model = linkedSignal<RoleDraft>(() => {
    const role = this.role();
    return role === null
      ? empty
      : {
          code: role.code,
          nameEn: role.nameEn,
          nameAr: role.nameAr ?? '',
          privilegeLevel: role.privilegeLevel,
        };
  });

  protected readonly creating = computed(() => this.role() === null);
  protected readonly levels: readonly PrivilegeLevel[] = Object.values(PrivilegeLevel);
  /** The permissions a new role starts with. */
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly form = form(this.model, (path) => {
    required(path.code, { message: wording('10202') });
    maxLength(path.code, CODE_MAX_LENGTH, { message: wording('10203') });
    pattern(path.code, roleCode, { message: wording('00006') });
    required(path.nameEn, { message: wording('10204') });
    maxLength(path.nameEn, NAME_MAX_LENGTH, { message: wording('10205') });
    maxLength(path.nameAr, NAME_MAX_LENGTH, { message: wording('10206') });
  });
  protected readonly problem = signal<string | null>(null);
  protected readonly permissionsProblem = signal<string | null>(null);

  constructor() {
    this.store.loadCatalogue();
    afterNextRender(() => {
      this.codeBox().nativeElement.focus();
    });
  }

  protected levelLabel(level: PrivilegeLevel): string {
    return privilegeLabel(level);
  }

  protected toggle(id: string): void {
    this.permissionsProblem.set(null);
    this.selected.update((set) => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.problem.set(null);
      const value = field().value();
      const draft: RoleDraft = {
        code: value.code.trim(),
        nameEn: value.nameEn.trim(),
        nameAr: value.nameAr.trim(),
        privilegeLevel: value.privilegeLevel,
      };
      const role = this.role();
      const permissionIds = Array.from(this.selected());
      if (role === null && permissionIds.length === 0) {
        this.permissionsProblem.set(wording('10208'));
        return undefined;
      }
      const result =
        role === null
          ? await this.store.create(draft, permissionIds, newIntentKey())
          : await this.store.update(role, draft, newIntentKey());
      return this.settle(result, field);
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  private settle(
    result: ApiResult<unknown>,
    field: typeof this.form,
  ):
    | {
        kind: string;
        message: string;
        fieldTree: typeof field.code | typeof field.privilegeLevel;
      }[]
    | undefined {
    if (result.kind === 'ok') {
      this.done.emit();
      return undefined;
    }
    if ('codes' in result) {
      const onFields = result.codes.flatMap((code) => {
        const name = fieldOfCode[code];
        return name === undefined
          ? []
          : [{ kind: `server-${code}`, message: wording(code), fieldTree: field[name] }];
      });
      if (onFields.length > 0) return onFields;
    }
    this.problem.set(messageOf(result));
    return undefined;
  }
}
