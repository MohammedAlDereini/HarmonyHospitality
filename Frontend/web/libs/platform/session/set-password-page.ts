import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, newIntentKey } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from './session-store';

/** Identity's anonymous door: the token from a reset or invitation mail, used once; every session of the account ends. */
export const resetPasswordPath = '/api/UserAccount/ResetPassword';

/** Identity's rule, mirrored for speed: 12 characters at least, one of them neither a letter nor a digit. */
const MIN_LENGTH = 12;

// Anything that is not a letter or a digit: a symbol or a space. Matches "!" and " "; rejects "a" and "7".
// Tester: https://regex101.com/?regex=%5B%5EA-Za-z0-9%5D&testString=Orchard-Chosen
const symbol = /[^A-Za-z0-9]/;

export type SetPasswordMode = 'set' | 'reset';

/**
 * Where the reset mail ("Choose a new password") and the invitation mail ("Choose your password") land
 * (architecture 10: a session page, outside the shell). The e-mail and the token ride in the link; the person types
 * the password twice; Identity checks the token and its own rules and ends every open session of the account.
 */
@Component({
  selector: 'hr-set-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
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
    .who {
      overflow-wrap: anywhere;
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
      @if (done()) {
        <h1 i18n="@@platform.session.set-password.done">Your password is set</h1>
        <p i18n="@@platform.session.set-password.done-text">
          Sign in with it now. Every earlier sign-in of this account has ended.
        </p>
        <p>
          <button type="button" (click)="signIn()" i18n="@@platform.session.set-password.sign-in">
            Sign in
          </button>
        </p>
      } @else if (!linkComplete()) {
        <h1 i18n="@@platform.session.set-password.incomplete-title">This link is incomplete</h1>
        <p i18n="@@platform.session.set-password.incomplete">
          It carries no address or no token. Open the link from the mail again, or ask for a new
          one.
        </p>
        <p>
          <a routerLink="/forgot-password" i18n="@@platform.session.set-password.again"
            >Ask for a new link</a
          >
        </p>
      } @else {
        @if (mode() === 'set') {
          <h1 i18n="@@platform.session.set-password.title-set">Choose your password</h1>
        } @else {
          <h1 i18n="@@platform.session.set-password.title-reset">Choose a new password</h1>
        }
        <p class="who" dir="ltr">{{ email() ?? '' }}</p>
        <form (submit)="save($event)" novalidate>
          <input type="text" autocomplete="username" [value]="email()" hidden readonly />
          <div class="f">
            <label for="set-password" i18n="@@platform.session.set-password.new"
              >New password</label
            >
            <input
              id="set-password"
              #first
              type="password"
              autocomplete="new-password"
              [value]="password()"
              (input)="password.set(first.value)"
              [attr.aria-invalid]="problem() !== null ? 'true' : null"
              [attr.aria-describedby]="
                'set-password-note' + (problem() !== null ? ' set-password-problem' : '')
              "
            />
            <p id="set-password-note" class="hint" i18n="@@platform.session.set-password.note">
              At least 12 characters with one symbol, like - or ! (a space counts). A few words make
              a strong password.
            </p>
          </div>
          <div class="f">
            <label for="set-password-repeat" i18n="@@platform.session.set-password.repeat"
              >Repeat it</label
            >
            <input
              id="set-password-repeat"
              #second
              type="password"
              autocomplete="new-password"
              [value]="repeat()"
              (input)="repeat.set(second.value)"
            />
          </div>
          @if (problem(); as text) {
            <p id="set-password-problem" class="field-error" role="alert">{{ text }}</p>
          }
          <p class="acts">
            <button type="submit" [disabled]="busy()" i18n="@@platform.session.set-password.save">
              Save the password
            </button>
            <a routerLink="/forgot-password" i18n="@@platform.session.set-password.again"
              >Ask for a new link</a
            >
          </p>
        </form>
      }
    </main>
  `,
})
export class SetPasswordPage {
  /** From the mail's link (query parameters bound by the router). */
  readonly email = input<string | undefined>('');
  readonly token = input<string | undefined>('');
  /** From the route: the invitation's first password ("set") or a replacement ("reset"). */
  readonly mode = input<SetPasswordMode>('reset');

  private readonly api = inject(ApiClient);
  private readonly session = inject(SessionStore);

  protected readonly password = signal('');
  protected readonly repeat = signal('');
  protected readonly problem = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly done = signal(false);
  protected readonly linkComplete = computed(
    () => (this.email() ?? '').trim() !== '' && (this.token() ?? '').trim() !== '',
  );

  protected signIn(): void {
    this.session.signIn('/');
  }

  protected save(event: Event): void {
    event.preventDefault();
    void this.saveNow();
  }

  private async saveNow(): Promise<void> {
    const password = this.password();
    if (password.length < MIN_LENGTH || !symbol.test(password)) {
      this.problem.set(
        $localize`:@@platform.session.set-password.weak:At least 12 characters with one symbol, like - or ! (a space counts).`,
      );
      return;
    }
    if (password !== this.repeat()) {
      this.problem.set(
        $localize`:@@platform.session.set-password.mismatch:The two passwords differ. Type the same one twice.`,
      );
      return;
    }
    this.problem.set(null);
    this.busy.set(true);
    const result = await firstValueFrom(
      this.api.post<unknown>(
        resetPasswordPath,
        { email: (this.email() ?? '').trim(), token: this.token() ?? '', newPassword: password },
        newIntentKey(),
      ),
    );
    this.busy.set(false);
    if (result.kind === 'ok') {
      this.done.set(true);
      return;
    }
    this.problem.set(messageOf(result));
  }
}
