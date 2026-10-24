import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, maxLength, required, submit } from '@angular/forms/signals';
import { UsersStore } from '@harmony/administration/data-access';
import { UserAccount } from '@harmony/administration/domain';
import { errorMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';

/** Identity accepts a reason of at most this many characters (ModelValidationErrorCodes 10304). */
const REASON_MAX_LENGTH = 500;
/** The platform's field codes that belong to the reason: they land under the field, not in the general line. */
const reasonCodes: readonly string[] = ['10303', '10304'];

/**
 * Suspends an account with a reason: a Signal Form (architecture 7.9), one intent key per press (11.5), the server's
 * field codes mapped back onto the field, every other outcome worded once from the code catalogue (14).
 */
@Component({
  selector: 'hr-account-state-editor',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './account-state-editor.html',
  styleUrl: './account-state-editor.css',
})
export class AccountStateEditor {
  readonly user = input.required<UserAccount>();
  /** The account changed; the row may close. */
  readonly done = output();
  /** The person gave up. */
  readonly cancelled = output();

  private readonly store = inject(UsersStore);
  private readonly reasonBox = viewChild.required<ElementRef<HTMLInputElement>>('reasonBox');
  private readonly model = signal({ reason: '' });

  protected readonly form = form(this.model, (path) => {
    required(path.reason, { message: $localize`:@@platform.errors.code.10303:Give a reason.` });
    maxLength(path.reason, REASON_MAX_LENGTH, {
      message: $localize`:@@platform.errors.code.10304:The reason is too long: 500 characters at most.`,
    });
  });

  /** One sentence for an outcome that is not about a field, or null while all is well. */
  protected readonly problem = signal<string | null>(null);

  constructor() {
    afterNextRender(() => {
      this.reasonBox().nativeElement.focus();
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.problem.set(null);
      // The key is this press's own: a retry inside the call reuses it, a new press is a new intent.
      const result = await this.store.suspend(
        this.user(),
        field().value().reason.trim(),
        newIntentKey(),
      );
      return this.settle(result, field);
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  /** Field codes go under the field; everything else becomes the general line; success closes the editor. */
  private settle(
    result: ApiResult<unknown>,
    field: typeof this.form,
  ): { kind: string; message: string; fieldTree: typeof field.reason }[] | undefined {
    if (result.kind === 'ok') {
      this.done.emit();
      return undefined;
    }
    if (result.kind === 'invalid') {
      const onField = result.codes.filter((code) => reasonCodes.includes(code));
      if (onField.length > 0) {
        return onField.map((code) => ({
          kind: `server-${code}`,
          message: errorMessage(code) ?? messageOf(result),
          fieldTree: field.reason,
        }));
      }
    }
    this.problem.set(messageOf(result));
    return undefined;
  }
}
