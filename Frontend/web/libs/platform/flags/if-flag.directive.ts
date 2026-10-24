import {
  Directive,
  TemplateRef,
  ViewContainerRef,
  effect,
  inject,
  input,
  untracked,
} from '@angular/core';
import { FlagsStore } from './flags-store';

/**
 * Shows its template only while the named flag is on: `<p *hrIfFlag="'night-audit'">…</p>`. Presentation only: a
 * flagged feature's doors stay guarded by the server whatever the browser shows.
 */
@Directive({ selector: '[hrIfFlag]' })
export class IfFlag {
  readonly hrIfFlag = input.required<string>();

  private readonly flags = inject(FlagsStore);
  private readonly template = inject<TemplateRef<unknown>>(TemplateRef);
  private readonly container = inject(ViewContainerRef);
  private shown = false;

  constructor() {
    effect(() => {
      const on = this.flags.on(this.hrIfFlag())();
      untracked(() => {
        if (on && !this.shown) {
          this.container.createEmbeddedView(this.template);
          this.shown = true;
        } else if (!on && this.shown) {
          this.container.clear();
          this.shown = false;
        }
      });
    });
  }
}
