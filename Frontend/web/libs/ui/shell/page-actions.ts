import { DestroyRef, Directive, Injectable, TemplateRef, inject, signal } from '@angular/core';

/**
 * The context bar's action slot (the prototype's .pacts): the page on screen offers its own buttons there, next to
 * the breadcrumb. One page at a time; the slot empties when the page leaves.
 */
@Injectable({ providedIn: 'root' })
export class PageActions {
  private readonly current = signal<TemplateRef<unknown> | null>(null);
  /** What the shell renders in the slot, or null while no page offers anything. */
  readonly template = this.current.asReadonly();

  take(template: TemplateRef<unknown>): void {
    this.current.set(template);
  }

  /** Frees the slot, unless a newer page took it already (the next page may be created before this one is destroyed). */
  release(template: TemplateRef<unknown>): void {
    if (this.current() === template) this.current.set(null);
  }
}

/**
 * Put on an ng-template inside a page: `<ng-template hrPageActions>…</ng-template>`. The template is rendered by the
 * shell in the context bar, with the page's own context, so its buttons call the page's methods.
 */
@Directive({ selector: 'ng-template[hrPageActions]' })
export class PageActionsTemplate {
  constructor() {
    const actions = inject(PageActions);
    const template = inject<TemplateRef<unknown>>(TemplateRef);
    actions.take(template);
    inject(DestroyRef).onDestroy(() => {
      actions.release(template);
    });
  }
}
