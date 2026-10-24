import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { AccessStore } from './access-store';
import { Permission } from './permission';

/**
 * *hrIfAllowed="Permission.ViewUsers": the content exists only while the person may do this. Presentation, not
 * security: the API refuses on its own.
 */
@Directive({ selector: '[hrIfAllowed]' })
export class IfAllowed {
  readonly permission = input.required<Permission>({ alias: 'hrIfAllowed' });
  private readonly access = inject(AccessStore);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);

  constructor() {
    effect(() => {
      const allowed = this.access.can(this.permission());
      this.container.clear();
      if (allowed) this.container.createEmbeddedView(this.template);
    });
  }
}
