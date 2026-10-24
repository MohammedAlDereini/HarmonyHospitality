import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  input,
  viewChild,
} from '@angular/core';

/** The weight of the question: a plain choice, or one that stops or loses something (red button). */
export type ConfirmTone = 'plain' | 'stop';

let nextId = 0;

/**
 * The review step before an action the business cannot undo (WCAG 3.3.4, architecture 8.3 overlays): a native modal
 * dialog, so the browser owns the top layer, the focus trap, Escape and the return of focus to the opener (the same
 * reasons the shell's menus are native popovers). The page embeds one and asks; the answer is a promise.
 *
 *   <hr-confirm-dialog #leave heading="…" message="…" go="Sign in here" stay="Stay" />
 *   if (await this.leave().ask()) { … }
 */
@Component({
  selector: 'hr-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './confirm-dialog.html',
  styleUrl: './confirm-dialog.css',
})
export class ConfirmDialog {
  /** The question, as the dialog's heading (not `title`: that would also be the host element's tooltip). */
  readonly heading = input.required<string>();
  readonly message = input<string>('');
  /** The label of the button that goes ahead. */
  readonly go = input.required<string>();
  /** The label of the button that keeps things as they are; it has focus first, so Enter is the safe answer. */
  readonly stay = input.required<string>();
  readonly tone = input<ConfirmTone>('plain');

  private readonly box = viewChild.required<ElementRef<HTMLDialogElement>>('box');
  private readonly serial = String(++nextId);
  protected readonly headingId = computed(() => `hr-confirm-${this.serial}-heading`);
  protected readonly messageId = computed(() => `hr-confirm-${this.serial}-message`);
  private settle: ((answer: boolean) => void) | null = null;
  private outcome = false;

  /** Opens the dialog and answers when the person has chosen; Escape and a second ask while open count as staying. */
  ask(): Promise<boolean> {
    if (this.settle !== null) return Promise.resolve(false);
    const dialog = this.box().nativeElement;
    this.outcome = false;
    return new Promise((resolve) => {
      this.settle = resolve;
      if (typeof dialog.showModal === 'function') dialog.showModal();
      else dialog.setAttribute('open', '');
    });
  }

  /** A button: closes and settles at once (the native close event is not waited for, see Drawer). */
  protected answer(value: boolean): void {
    this.outcome = value;
    const dialog = this.box().nativeElement;
    if (typeof dialog.close === 'function') dialog.close();
    else dialog.removeAttribute('open');
    this.onClose();
  }

  /** The dialog closed, by a button, by Escape or by the browser: the waiting caller gets the answer, once. */
  protected onClose(): void {
    const settle = this.settle;
    this.settle = null;
    settle?.(this.outcome);
    this.outcome = false;
  }
}
