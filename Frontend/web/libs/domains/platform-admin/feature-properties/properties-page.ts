import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { PROPERTIES_PAGE_SIZE, PropertiesStore } from '@harmony/platform-admin/data-access';
import { Property, propertyTitle } from '@harmony/platform-admin/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { Pager } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageActionsTemplate } from '@harmony/ui/shell';
import { PropertyEditor } from './property-editor';

/**
 * Every customer's properties, or one tenant's when the URL says ?tenant=, the way the platform app listed things:
 * a lead, the search in a toolbar, a real table, a pager; a new property in the drawer from the context bar's
 * action slot (architecture 6, platform-admin).
 */
@Component({
  selector: 'hr-properties-page',
  imports: [RouterLink, IfAllowed, Icon, PageActionsTemplate, PropertyEditor, Pager, Drawer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './properties-page.html',
  styleUrl: './properties-page.css',
})
export class PropertiesPage {
  /** From the query string (?tenant=), bound by the router. */
  readonly tenant = input<string | undefined>();

  protected readonly store = inject(PropertiesStore);
  private readonly toasts = inject(Toasts);
  private readonly drawer = viewChild.required(Drawer);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly pageSize = PROPERTIES_PAGE_SIZE;
  protected readonly creating = signal(false);
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly tenantId = computed(() => this.tenant() ?? null);

  constructor() {
    effect(() => {
      const tenantId = this.tenantId();
      untracked(() => {
        this.store.ofTenant(tenantId);
      });
    });
  }

  protected name(property: Property): string {
    return propertyTitle(property, this.locale);
  }

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.store.search(text);
  }

  protected openCreator(): void {
    this.creating.set(true);
    this.drawer().open();
  }

  protected saved(): void {
    this.toasts.show($localize`:@@platform-admin.properties.toast.added:Property added.`);
    this.drawer().close();
  }
}
