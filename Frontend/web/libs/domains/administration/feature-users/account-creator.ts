import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, email, form, maxLength, required, submit } from '@angular/forms/signals';
import { RolesStore, UsersStore } from '@harmony/administration/data-access';
import { NewAccount, RoleOption, roleName } from '@harmony/administration/domain';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';

/** Identity's limits (CreateUserAccountCommand): the name 128, the email 256 characters. */
const NAME_MAX_LENGTH = 128;
const EMAIL_MAX_LENGTH = 256;
/** The field each of Identity's codes belongs to; any other code becomes the general line. */
const fieldOfCode: Readonly<Record<string, keyof NewAccount>> = {
  '10308': 'displayName',
  '10309': 'displayName',
  '10310': 'email',
  '10311': 'email',
  '10312': 'email',
  '10315': 'roleId',
  '10316': 'roleId',
};
/** The catalogue's wording for a code; the client rules mirror the server's for speed only (architecture 7.9). */
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/**
 * A new account: name, email and one role; no password, Identity mails the invitation. A Signal Form, one intent
 * key per press, Identity's field codes back under their fields, every other outcome worded once (11.5, 14).
 */
@Component({
  selector: 'hr-account-creator',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './account-creator.html',
  styleUrl: './account-creator.css',
})
export class AccountCreator {
  /** The account exists; the form may close. */
  readonly done = output();
  /** The person gave up. */
  readonly cancelled = output();

  protected readonly roles = inject(RolesStore);
  private readonly users = inject(UsersStore);
  private readonly nameBox = viewChild.required<ElementRef<HTMLInputElement>>('nameBox');
  private readonly locale = $localize.locale ?? 'en';
  private readonly model = signal<NewAccount>({ displayName: '', email: '', roleId: '' });

  protected readonly form = form(this.model, (path) => {
    required(path.displayName, { message: wording('10308') });
    maxLength(path.displayName, NAME_MAX_LENGTH, { message: wording('10309') });
    required(path.email, { message: wording('10310') });
    email(path.email, { message: wording('10311') });
    maxLength(path.email, EMAIL_MAX_LENGTH, { message: wording('10312') });
    required(path.roleId, { message: wording('10315') });
  });

  /** One sentence for an outcome that is not about a field, or null while all is well. */
  protected readonly problem = signal<string | null>(null);

  constructor() {
    this.roles.load();
    afterNextRender(() => {
      this.nameBox().nativeElement.focus();
    });
  }

  protected label(role: RoleOption): string {
    return roleName(role, this.locale);
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.problem.set(null);
      const value = field().value();
      const account: NewAccount = {
        displayName: value.displayName.trim(),
        email: value.email.trim(),
        roleId: value.roleId,
      };
      // The key is this press's own: a retry inside the call reuses it, a new press is a new intent.
      const result = await this.users.create(account, newIntentKey());
      return this.settle(result, field);
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  /** Field codes go under their fields; everything else becomes the general line; success closes the form. */
  private settle(
    result: ApiResult<unknown>,
    field: typeof this.form,
  ): { kind: string; message: string; fieldTree: typeof field.displayName }[] | undefined {
    if (result.kind === 'ok') {
      this.done.emit();
      return undefined;
    }
    if (result.kind === 'invalid') {
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
