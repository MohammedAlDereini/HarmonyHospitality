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
import { GROUPS_PAGE_SIZE, GroupsStore, ReferenceStore } from '@harmony/platform-admin/data-access';
import { PropertyGroup, ReferenceRow, groupTitle } from '@harmony/platform-admin/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { Pager } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageActionsTemplate } from '@harmony/ui/shell';
import { GroupEditor } from './group-editor';

/**
 * Every customer's hotel groups (brands, regions, owners), or one tenant's when the URL says ?tenant=: a lead, the
 * search and the type filter in a toolbar, a real table, a pager; a new group in the drawer from the context bar's
 * action slot (architecture 6, platform-admin).
 */
@Component({
  selector: 'hr-groups-page',
  imports: [RouterLink, IfAllowed, Icon, PageActionsTemplate, GroupEditor, Pager, Drawer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './groups-page.html',
  styleUrl: './groups-page.css',
})
export class GroupsPage {
  /** From the query string (?tenant=), bound by the router. */
  readonly tenant = input<string | undefined>();

  protected readonly store = inject(GroupsStore);
  private readonly reference = inject(ReferenceStore);
  private readonly toasts = inject(Toasts);
  private readonly drawer = viewChild.required(Drawer);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly pageSize = GROUPS_PAGE_SIZE;
  protected readonly creating = signal(false);
  protected readonly page = computed(() => this.store.filters().page);
  protected readonly typeValue = computed(() => this.store.filters().typeCode ?? '');
  protected readonly tenantId = computed(() => this.tenant() ?? null);
  protected readonly types = this.reference.choices('PropertyGroupType');

  constructor() {
    this.types.load();
    effect(() => {
      const tenantId = this.tenantId();
      untracked(() => {
        this.store.ofTenant(tenantId);
      });
    });
  }

  protected name(group: PropertyGroup): string {
    return groupTitle(group, this.locale);
  }

  protected typeLabel(row: ReferenceRow): string {
    return String(row['nameEn'] ?? row['code'] ?? '');
  }

  protected typeCode(row: ReferenceRow): string {
    return String(row['code'] ?? '');
  }

  protected onSearch(event: Event, text: string): void {
    event.preventDefault();
    this.store.search(text);
  }

  protected onType(value: string): void {
    this.store.ofType(value === '' ? null : value);
  }

  protected openCreator(): void {
    this.creating.set(true);
    this.drawer().open();
  }

  protected saved(): void {
    this.toasts.show($localize`:@@platform-admin.groups.toast.added:Group added.`);
    this.drawer().close();
  }
}
