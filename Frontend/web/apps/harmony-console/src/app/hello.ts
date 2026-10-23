import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'hr-hello',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1 i18n="@@harmony-console.hello.title">Hello from a deep link</h1>`,
})
export class Hello {}
