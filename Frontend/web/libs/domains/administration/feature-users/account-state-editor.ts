import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  computed,
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
import { ApiResult, IntentKey, newIntentKey } from '@harmony/platform/http';

/** The writes that take a reason: suspension can be undone, termination is final, exemption goes on the two-step list. */
export type AccountAction = 'suspend' | 'terminate' | 'exempt';

/** Identity accepts a reason of at most this many characters (ModelValidationErrorCodes 10304). */
const REASON_MAX_LENGTH = 500;
/** The platform's field codes that belong to the reason: they land under the field, not in the general line. */
const reasonCodes: readonly string[] = ['10303', '10304'];

interface Wording {
  readonly label: string;
  readonly placeholder: string;
  readonly go: string;
  /** Said before an action that cannot be undone (WCAG 3.3.4: the reason box is the review step). */
  readonly warning: string | null;
}

const wordingOf: Readonly<Record<AccountAction, Wording>> = {
  suspend: {
    label: $localize`:@@administration.users.suspend.reason:Reason for the suspension`,
    placeholder: $localize`:@@administration.users.suspend.placeholder:Why this account is being suspended`,
    go: $localize`:@@administration.users.suspend.go:Suspend account`,
    warning: null,
  },
  terminate: {
    label: $localize`:@@administration.users.terminate.reason:Reason for the termination`,
    placeholder: $localize`:@@administration.users.terminate.placeholder:Why this account is being terminated`,
    go: $localize`:@@administration.users.terminate.go:Terminate account`,
    warning: $localize`:@@administration.users.terminate.warning:This cannot be undone: the person loses access for good.`,
  },
  exempt: {
    label: $localize`:@@administration.users.exempt.reason:Reason for the exemption`,
    placeholder: $localize`:@@administration.users.exempt.placeholder:Why this person is on the two-step exception list`,
    go: $localize`:@@administration.users.exempt.go:Exempt from two-step`,
    warning: null,
  },
};

type Writer = (user: UserAccount, reason: string, intent: IntentKey) => Promise<ApiResult<unknown>>;

/**
 * Suspends or terminates an account with a reason: a Signal Form (architecture 7.9), one intent key per press
 * (11.5), the server's field codes mapped back onto the field, every other outcome worded once from the code
 * catalogue (14).
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
  readonly mode = input<AccountAction>('suspend');
  /** The account changed; the row may close. */
  readonly done = output();
  /** The person gave up. */
  readonly cancelled = output();

  private readonly store = inject(UsersStore);
  private readonly reasonBox = viewChild.required<ElementRef<HTMLInputElement>>('reasonBox');
  private readonly model = signal({ reason: '' });
  private readonly writers: Readonly<Record<AccountAction, Writer>> = {
    suspend: (user, reason, intent) => this.store.suspend(user, reason, intent),
    terminate: (user, reason, intent) => this.store.terminate(user, reason, intent),
    exempt: (user, reason, intent) => this.store.exempt(user, reason, intent),
  };

  protected readonly wording = computed(() => wordingOf[this.mode()]);
  protected readonly final = computed(() => this.mode() === 'terminate');

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
      const write = this.writers[this.mode()];
      const result = await write(this.user(), field().value().reason.trim(), newIntentKey());
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
