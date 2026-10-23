import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  afterNextRender,
  inject,
} from '@angular/core';
import { SessionChannel } from './session-channel';

/**
 * Where the separate sign-in window lands (architecture 11.2): it tells the original tab over the channel and
 * closes itself. A window the browser will not close keeps the sentence below.
 */
@Component({
  selector: 'hr-signed-in',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p i18n="@@platform.session.signed-in">
    You are signed in. You can close this window.
  </p>`,
})
export class SignedInPage {
  constructor() {
    inject(SessionChannel).post({ type: 'signed-in' });
    const window = inject(DOCUMENT).defaultView;
    afterNextRender(() => {
      window?.close();
    });
  }
}
