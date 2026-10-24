import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, maxLength, required, submit } from '@angular/forms/signals';
import { errorMessage, kindMessage } from '@harmony/platform/errors';

/** Identity accepts a reason of at most this many characters (ModelValidationErrorCodes 10514). */
const REASON_MAX_LENGTH = 500;
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/**
 * One reason, then one press: the parent names the action and makes the call; a refusal comes back through
 * `problem`, and the field codes 10513 / 10514 land under the field (architecture 7.9, 14).
 */
@Component({
  selector: 'hr-reason-form',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reason-form.html',
  styleUrl: './reason-form.css',
})
export class ReasonForm {
  readonly label = input.required<string>();
  readonly placeholder = input.required<string>();
  readonly go = input.required<string>();
  /** Said before an action that cannot be undone. */
  readonly warning = input<string | null>(null);
  readonly busy = input(false);
  /** The parent's one sentence when its call was refused. */
  readonly problem = input<string | null>(null);
  readonly submitted = output<string>();
  readonly cancelled = output();

  private readonly box = viewChild.required<ElementRef<HTMLInputElement>>('box');
  private readonly model = signal({ reason: '' });
  protected readonly form = form(this.model, (path) => {
    required(path.reason, { message: wording('10513') });
    maxLength(path.reason, REASON_MAX_LENGTH, { message: wording('10514') });
  });

  constructor() {
    afterNextRender(() => {
      this.box().nativeElement.focus();
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.submitted.emit(field().value().reason.trim());
      return Promise.resolve(undefined);
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }
}
