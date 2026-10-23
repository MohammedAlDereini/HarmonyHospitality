import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'hr-home',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 i18n="@@harmony-console.home.title">Harmony console</h1>
    <p><a routerLink="/hello" i18n="@@harmony-console.home.hello-link">A deep link</a></p>
  `,
})
export class Home {}