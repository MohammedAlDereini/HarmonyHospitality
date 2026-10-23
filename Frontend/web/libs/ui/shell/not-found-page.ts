import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/** The catch-all route (architecture 10): a plain page with one way to the start. */
@Component({
  selector: 'hr-not-found',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main>
      <h1 i18n="@@ui.shell.not-found.title">This page does not exist</h1>
      <p><a routerLink="/" i18n="@@ui.shell.not-found.home">Go to the start</a></p>
    </main>
  `,
})
export class NotFoundPage {}
