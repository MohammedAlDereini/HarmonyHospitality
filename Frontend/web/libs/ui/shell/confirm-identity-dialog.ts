import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { IdentityConfirmation, SessionStore } from '@harmony/platform/session';

/** Identity's amr value for a sign-in made at the person's company (Microsoft or Google). */
const COMPANY_SIGN_IN = 'external';

/**
 * The shell's one "Confirm it is you" question (stage B): a native modal dialog that opens while IdentityConfirmation
 * asks, takes the password, and answers. Escape and Cancel stay. A company person has no Harmony password: the dialog
 * says so instead of asking for one. The password is read from the box when sent and the box is emptied at once; it is
 * never kept in the component's state (and the shell, part of the first load, needs no form library for one box).
 */
@Component({
  selector: 'hr-confirm-identity',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './confirm-identity-dialog.html',
  styleUrl: './confirm-identity-dialog.css',
})
export class ConfirmIdentityDialog {
  protected readonly confirmation = inject(IdentityConfirmation);
  private readonly session = inject(SessionStore);
  private readonly box = viewChild.required<ElementRef<HTMLDialogElement>>('box');
  private readonly passwordBox = viewChild<ElementRef<HTMLInputElement>>('password');

  protected readonly viaCompany = computed(
    () => this.session.user()?.amr.includes(COMPANY_SIGN_IN) ?? false,
  );
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);

  constructor() {
    effect(() => {
      const dialog = this.box().nativeElement;
      if (this.confirmation.asking()) {
        this.problem.set(null);
        this.clear();
        if (typeof dialog.showModal === 'function') dialog.showModal();
        else dialog.setAttribute('open', '');
      } else if (dialog.hasAttribute('open')) {
        if (typeof dialog.close === 'function') dialog.close();
        else dialog.removeAttribute('open');
      }
    });
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    const password = this.passwordBox()?.nativeElement.value ?? '';
    this.clear();
    if (password === '') {
      this.problem.set(
        $localize`:@@platform.session.confirm.password-required:Type your password.`,
      );
      return;
    }
    this.problem.set(null);
    this.busy.set(true);
    const result = await this.confirmation.confirm(password, newIntentKey());
    this.busy.set(false);
    if (result.kind !== 'ok') this.problem.set(messageOf(result));
  }

  /** Cancel, Escape or the browser closed it: the waiting change does not go ahead. */
  protected stay(): void {
    this.clear();
    this.confirmation.dismiss();
  }

  private clear(): void {
    const input = this.passwordBox()?.nativeElement;
    if (input !== undefined) input.value = '';
  }
}
