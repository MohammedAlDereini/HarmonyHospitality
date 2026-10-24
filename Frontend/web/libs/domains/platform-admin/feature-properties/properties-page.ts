import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { PROPERTIES_PAGE_SIZE, PropertiesStore } from '@harmony/platform-admin/data-access';
import { Property, propertyTitle } from '@harmony/platform-admin/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { Icon } from '@harmony/ui/icons';
import { PageActionsTemplate } from '@harmony/ui/shell';
import { PropertyEditor } from './property-editor';

const PAGE_STEP = 1;

/**
 * Every customer's properties, or one tenant's when the URL says ?tenant=: code, name, where, rooms, active; a new
 * property from the context bar's action slot (architecture 6, platform-admin).
 */
@Component({
  selector: 'hr-properties-page',
  imports: [RouterLink, IfAllowed, Icon, PageActionsTemplate, PropertyEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './properties-page.html',
  styleUrl: './properties-page.css',
})
export class PropertiesPage {
  /** From the query string (?tenant=), bound by the router. */
  readonly tenant = input<string | undefined>();

  protected readonly store = inject(PropertiesStore);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly creating = signal(false);
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly from = computed(
    () => (this.page() - PAGE_STEP) * PROPERTIES_PAGE_SIZE + PAGE_STEP,
  );
  protected readonly to = computed(() =>
    Math.min(this.page() * PROPERTIES_PAGE_SIZE, this.store.total()),
  );
  protected readonly hasPrevious = computed(() => this.page() > PAGE_STEP);
  protected readonly hasNext = computed(() => this.to() < this.store.total());
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

  protected previous(): void {
    this.store.goTo(this.page() - PAGE_STEP);
  }

  protected next(): void {
    this.store.goTo(this.page() + PAGE_STEP);
  }

  protected openCreator(): void {
    this.creating.set(true);
  }

  protected closeCreator(): void {
    this.creating.set(false);
  }
}
