import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, newIntentKey } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from './session-store';

/** Identity's anonymous door: it always answers "ok", so nobody learns which addresses exist. */
export const forgotPasswordPath = '/api/UserAccount/ForgotPassword';

// A plausible address: something, an @, something, a dot, something. Matches "lina@ritz.test"; rejects "lina@ritz".
// Tester: https://regex101.com/?regex=%5E%5B%5E%5Cs%40%5D%2B%40%5B%5E%5Cs%40%5D%2B%5C.%5B%5E%5Cs%40%5D%2B%24&testString=lina%40ritz.test
const emailShape = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * The sign-in page's "Forgot your password?" lands here (architecture 10: a session page, outside the shell). One
 * field, one press; the answer is the same whatever the address, and the mail carries the link to the page that
 * takes the new password.
 */
@Component({
  selector: 'hr-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    main {
      max-inline-size: 28rem;
      margin: 10vh auto 0;
      padding-inline: var(--s4);
      display: grid;
      gap: var(--s3);
    }
    .f {
      display: grid;
      gap: var(--s1);
    }
    input {
      min-height: var(--control-md);
    }
    .acts {
      display: flex;
      gap: var(--s2);
      align-items: center;
      flex-wrap: wrap;
    }
  `,
  template: `
    <main>
      @if (sent()) {
        <h1 i18n="@@platform.session.forgot.sent">Check your inbox</h1>
        <p i18n="@@platform.session.forgot.sent-text">
          If that address has an account, the mail is on its way. It can take a minute; the link
          works once.
        </p>
        <p>
          <button type="button" (click)="signIn()" i18n="@@platform.session.forgot.back">
            Back to sign in
          </button>
        </p>
      } @else {
        <h1 i18n="@@platform.session.forgot.title">Forgot your password?</h1>
        <p i18n="@@platform.session.forgot.lead">
          Type your work e-mail. If it belongs to an account, a link to choose a new password is on
          its way; it works once and for a short while.
        </p>
        <form (submit)="send($event)" novalidate>
          <div class="f">
            <label for="forgot-email" i18n="@@platform.session.forgot.email">Work e-mail</label>
            <input
              id="forgot-email"
              #box
              type="email"
              autocomplete="username"
              dir="ltr"
              [value]="email()"
              (input)="email.set(box.value)"
              [attr.aria-invalid]="problem() !== null ? 'true' : null"
              [attr.aria-describedby]="problem() !== null ? 'forgot-problem' : null"
            />
            @if (problem(); as text) {
              <p id="forgot-problem" class="field-error" role="alert">{{ text }}</p>
            }
          </div>
          <p class="acts">
            <button type="submit" [disabled]="busy()" i18n="@@platform.session.forgot.send">
              Send the link
            </button>
            <button type="button" (click)="signIn()" i18n="@@platform.session.forgot.back">
              Back to sign in
            </button>
          </p>
        </form>
      }
    </main>
  `,
})
export class ForgotPasswordPage {
  private readonly api = inject(ApiClient);
  private readonly session = inject(SessionStore);

  protected readonly email = signal('');
  protected readonly problem = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly sent = signal(false);

  protected signIn(): void {
    this.session.signIn('/');
  }

  protected send(event: Event): void {
    event.preventDefault();
    void this.sendNow();
  }

  /** One press at a time; the door answers ok for any address, so "sent" means "asked", never "found". */
  private async sendNow(): Promise<void> {
    const email = this.email().trim();
    if (email === '') {
      this.problem.set($localize`:@@platform.session.forgot.email-required:Give your work e-mail.`);
      return;
    }
    if (!emailShape.test(email)) {
      this.problem.set(
        $localize`:@@platform.session.forgot.email-shape:That does not look like an email address.`,
      );
      return;
    }
    this.problem.set(null);
    this.busy.set(true);
    const result = await firstValueFrom(
      this.api.post<unknown>(forgotPasswordPath, { email }, newIntentKey()),
    );
    this.busy.set(false);
    if (result.kind === 'ok') {
      this.sent.set(true);
      return;
    }
    this.problem.set(messageOf(result));
  }
}
