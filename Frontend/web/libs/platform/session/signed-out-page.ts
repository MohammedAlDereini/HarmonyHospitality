import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { SessionStore } from './session-store';

/** After sign-out (architecture 10): a quiet page with one way back in. */
@Component({
  selector: 'hr-signed-out',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main>
      <h1 i18n="@@platform.session.signed-out.title">You have signed out</h1>
      <p>
        <button type="button" (click)="signIn()" i18n="@@platform.session.signed-out.sign-in">
          Sign in
        </button>
      </p>
    </main>
  `,
})
export class SignedOutPage {
  private readonly session = inject(SessionStore);

  signIn(): void {
    this.session.signIn('/');
  }
}
