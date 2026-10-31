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
import {
  OptionRows,
  REFERENCE_PAGE_SIZE,
  ReferenceFilters,
  ReferenceStore,
  TableCount,
  TableRows,
} from '@harmony/platform-admin/data-access';
import {
  ReferenceColumn,
  ReferenceFilter,
  ReferenceOption,
  ReferenceRow,
  ReferenceTable,
} from '@harmony/platform-admin/domain';
import { RouterLink } from '@angular/router';
import { AccessStore, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { SortOrder } from '@harmony/shared/domain';
import { AriaSort, Facet, FacetOption, Pager, SortButton, ariaSort } from '@harmony/ui/data';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead } from '@harmony/ui/shell';
import { RowEditor } from './row-editor';
import { tableByKey, tables } from './tables';

/** A table read per category is never counted as a whole. */
const scopedCount: TableCount = { total: signal('scoped') };

/** What the drawer holds: a row being changed, or null for a new one. */
interface Editing {
  readonly row: ReferenceRow | null;
}

const asText = (
  value: string | number | boolean | readonly string[] | null | undefined,
): string => {
  if (value === null || value === undefined) return '';
  return Array.isArray(value) ? value.join(', ') : String(value);
};
const capital = (text: string): string => text.charAt(0).toLocaleUpperCase() + text.slice(1);
/** Only active rows, until the switch includes the inactive ones. */
const ACTIVE_ONLY = 'true';
const stampFormat: Intl.DateTimeFormatOptions = {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  timeZone: 'UTC',
};

/**
 * One reference table the way the platform app showed it (architecture 6, platform-admin): the title and what the
 * table is for, the filter toolbar, a real table with sortable columns, a pager, Add in the context bar; a row is
 * added or changed in the drawer at the end of the page; a lookup row is deleted after a confirmation. Lookup values
 * are read for one category at a time.
 */
@Component({
  selector: 'hr-reference-table-page',
  imports: [RouterLink, Icon, Facet, RowEditor, Pager, SortButton, Drawer, ConfirmDialog, PageHead],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reference-table-page.html',
  styleUrl: './reference-table-page.css',
})
export class ReferenceTablePage {
  /** From the route (:table), bound by the router. */
  readonly table = input.required<string>();

  private readonly store = inject(ReferenceStore);
  private readonly access = inject(AccessStore);
  private readonly toasts = inject(Toasts);
  private readonly drawer = viewChild.required(Drawer);
  private readonly confirm = viewChild.required(ConfirmDialog);

  protected readonly pageSize = REFERENCE_PAGE_SIZE;
  /** Every table with its live count: the rail on the left. */
  protected readonly rail = tables.map((table) => ({
    table,
    count: table.scopedBy === undefined ? this.store.count(table.controller) : scopedCount,
  }));
  private readonly locale = $localize.locale ?? 'en';
  private readonly stamps = new Intl.DateTimeFormat(
    this.locale === 'en' ? 'en-GB' : this.locale,
    stampFormat,
  );
  protected readonly description = computed<ReferenceTable | null>(() => tableByKey(this.table()));
  protected readonly rows = computed<TableRows | null>(() => {
    const description = this.description();
    return description === null
      ? null
      : this.store.table(description.controller, description.defaultSort);
  });
  /** Lookup values: the categories to choose from. */
  protected readonly categories = this.store.choices('LookupCategory');
  protected readonly category = signal('');
  /** A new row of a scoped table starts in the category the list shows. */
  protected readonly newRowDefaults = computed<Readonly<Record<string, string>>>(() => {
    const scopedBy = this.description()?.scopedBy;
    const id = this.category();
    return scopedBy !== undefined && id !== '' ? { [scopedBy]: id } : {};
  });
  protected readonly mayManage = computed(() => {
    const description = this.description();
    if (description === null) return false;
    return this.access.can(
      description.lookup === true ? Permission.ManageLookups : Permission.ManageTenants,
    );
  });
  protected readonly editing = signal<Editing | null>(null);
  protected readonly removing = signal<string | null>(null);
  protected readonly actionProblem = signal<string | null>(null);
  /** The filters as typed, until Apply. */
  protected readonly draft = signal<ReferenceFilters>({});
  /** Something narrows the list beyond the starting view (active rows only is that view, not a filter). */
  protected readonly hasFilters = computed(() =>
    Object.entries(this.rows()?.filters() ?? {}).some(([key, value]) =>
      key === 'isActive' ? value !== ACTIVE_ONLY : value !== '',
    ),
  );
  /** The switch is on when inactive rows are not filtered out. */
  protected readonly includeInactive = computed(
    () => (this.draft()['isActive'] ?? '') !== ACTIVE_ONLY,
  );
  /** Every row the table holds, whatever the filters say; the filtered total until the count is known. */
  protected readonly allCount = computed(() => {
    const description = this.description();
    if (description === null) return 0;
    if (description.scopedBy !== undefined) return this.rows()?.total() ?? 0;
    const total = this.store.count(description.controller).total();
    return typeof total === 'number' ? total : (this.rows()?.total() ?? 0);
  });
  protected readonly searchPlaceholder = computed(() => {
    const description = this.description();
    const what = description === null ? '' : description.title.toLocaleLowerCase();
    return $localize`:@@platform-admin.reference.search-in:Search ${what}:TABLE:`;
  });
  /** The column the rows are sorted by, in words; '' when Identity's default order stands unnamed. */
  protected readonly sortedBy = computed(() => {
    const description = this.description();
    const sort = this.rows()?.sort() ?? null;
    if (description === null || sort === null) return '';
    return (
      description.columns
        .find((column) => column.sort === sort.memberName)
        ?.label.toLocaleLowerCase() ?? ''
    );
  });
  protected readonly keyColumn = computed(() => {
    const description = this.description();
    if (description === null) return '';
    return description.fields.find((field) => field.isKey)?.key ?? description.idField;
  });
  protected readonly addLabel = computed(() => {
    const description = this.description();
    return description === null
      ? ''
      : $localize`:@@platform-admin.reference.add-one:Add ${description.singular}:SINGULAR:`;
  });
  protected readonly drawerHeading = computed(() => {
    const description = this.description();
    if (description === null) return '';
    const row = this.editing()?.row ?? null;
    return row === null
      ? this.addLabel()
      : $localize`:@@platform-admin.reference.edit-named:Edit ${description.singular}:SINGULAR: · ${this.name(row)}:NAME:`;
  });
  protected readonly drawerLead = computed(() =>
    this.editing()?.row === null || this.editing()?.row === undefined
      ? $localize`:@@platform-admin.reference.key-note.new:The code or key is chosen once and then fixed.`
      : $localize`:@@platform-admin.reference.key-note.edit:The identity of the row (its code or key) cannot change: other rows point at it.`,
  );
  protected readonly deleteHeading = computed(() => {
    const description = this.description();
    return description === null
      ? ''
      : $localize`:@@platform-admin.reference.delete.heading:Delete this ${description.singular}:SINGULAR:?`;
  });
  protected readonly deleteMessage = $localize`:@@platform-admin.reference.delete.message:This cannot be undone.`;
  protected readonly deleteGo = $localize`:@@platform-admin.reference.delete.go:Delete`;
  protected readonly stay = $localize`:@@platform-admin.reference.cancel:Cancel`;
  /** The other tables a filter chooses from, read when the screen opens. */
  private readonly lookups = signal<ReadonlyMap<string, OptionRows>>(new Map());

  constructor() {
    effect(() => {
      const description = this.description();
      const rows = this.rows();
      untracked(() => {
        this.editing.set(null);
        this.category.set('');
        this.actionProblem.set(null);
        const known = rows?.filters() ?? {};
        const hasState = description?.filters.some((filter) => filter.key === 'isActive') ?? false;
        const starting =
          hasState && known['isActive'] === undefined ? { ...known, isActive: ACTIVE_ONLY } : known;
        this.draft.set(starting);
        if (starting !== known) rows?.query({ filters: starting });
        const lookups = new Map<string, OptionRows>();
        description?.filters.forEach((filter) => {
          const from = filter.optionsFrom;
          if (from === undefined) return;
          const source = this.store.choices(from.table);
          source.load();
          lookups.set(filter.key, source);
        });
        this.lookups.set(lookups);
        if (description?.scopedBy !== undefined) {
          this.categories.load();
          return;
        }
        rows?.load();
      });
    });
  }

  protected idOf(row: ReferenceRow): string {
    const description = this.description();
    return description === null ? '' : asText(row[description.idField]);
  }

  protected text(row: ReferenceRow, column: ReferenceColumn): string {
    return asText(row[column.key]);
  }

  protected flag(row: ReferenceRow, column: ReferenceColumn): boolean {
    return row[column.key] === true;
  }

  protected name(row: ReferenceRow): string {
    return asText(row['name'] ?? row['nameEn'] ?? row['id']);
  }

  protected sortOf(column: ReferenceColumn): AriaSort {
    const rows = this.rows();
    if (column.sort === undefined || rows === null) return null;
    return ariaSort(column.sort, rows.sort());
  }

  protected sortBy(order: SortOrder): void {
    this.rows()?.query({ sort: order });
  }

  protected goTo(page: number): void {
    this.rows()?.query({ page });
  }

  protected draftChange(key: string, event: Event): void {
    const value = (event.target as HTMLInputElement | HTMLSelectElement).value;
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }

  protected applyFilters(event: Event): void {
    event.preventDefault();
    this.rows()?.query({ filters: this.draft() });
  }

  /** Back to the starting view: no text, no facet, active rows only where the table knows a state. */
  protected clearFilters(): void {
    const hasState =
      this.description()?.filters.some((filter) => filter.key === 'isActive') ?? false;
    const starting: ReferenceFilters = hasState ? { isActive: ACTIVE_ONLY } : {};
    this.draft.set(starting);
    this.rows()?.query({ filters: starting });
  }

  /** A select or lookup filter as facet choices (the empty "All" choice is the facet's own Any). */
  protected facetOptions(filter: ReferenceFilter): readonly FacetOption[] {
    return this.filterOptions(filter)
      .filter((option) => option.value !== '')
      .map((option) => ({ value: option.value, label: option.label }));
  }

  protected facetValue(filter: ReferenceFilter): readonly string[] {
    const value = this.draft()[filter.key] ?? '';
    return value === '' ? [] : [value];
  }

  /** A facet applies at once. */
  protected onFacet(filter: ReferenceFilter, values: readonly string[]): void {
    this.draft.update((draft) => ({ ...draft, [filter.key]: values[0] ?? '' }));
    this.rows()?.query({ filters: this.draft() });
  }

  protected toggleInactive(include: boolean): void {
    this.draft.update((draft) => ({ ...draft, isActive: include ? '' : ACTIVE_ONLY }));
    this.rows()?.query({ filters: this.draft() });
  }

  /** "14 Sep 2026 10:22 UTC · Sara Al-Harbi" when Identity says who changed the row last; '' otherwise. */
  protected audit(row: ReferenceRow): string {
    const changed = row['modifiedDate'] ?? row['createdDate'];
    if (typeof changed !== 'string' || changed === '') return '';
    const by = row['modifiedBy'] ?? row['createdBy'];
    const when = `${this.stamps.format(new Date(changed.endsWith('Z') || changed.includes('+') ? changed : `${changed}Z`))} UTC`;
    return typeof by === 'string' && by !== '' ? `${when} · ${by}` : when;
  }

  protected filterOptions(filter: ReferenceFilter): readonly ReferenceOption[] {
    if (filter.options !== undefined) return filter.options;
    const from = filter.optionsFrom;
    const source = this.lookups().get(filter.key);
    if (from === undefined || source === undefined) return [];
    return source.rows().map((row) => ({
      value: asText(row[from.value]),
      label: `${asText(row[from.value])} · ${asText(row[from.label])}`,
    }));
  }

  protected chooseCategory(id: string): void {
    this.category.set(id);
    const description = this.description();
    if (description?.scopedBy !== undefined && id !== '') {
      this.rows()?.load({ [description.scopedBy]: id });
    }
  }

  protected openCreator(): void {
    this.actionProblem.set(null);
    this.editing.set({ row: null });
    this.drawer().open();
  }

  protected openEditor(row: ReferenceRow): void {
    this.actionProblem.set(null);
    this.editing.set({ row });
    this.drawer().open();
  }

  /** The drawer has closed, by Save, Cancel, Escape or the scrim: the form goes with it. */
  protected drawerClosed(): void {
    this.editing.set(null);
  }

  protected saved(): void {
    const description = this.description();
    if (description !== null) {
      const name = capital(description.singular);
      this.toasts.show(
        this.editing()?.row === null || this.editing()?.row === undefined
          ? $localize`:@@platform-admin.reference.toast.added:${name}:NAME: added.`
          : $localize`:@@platform-admin.reference.toast.saved:${name}:NAME: saved.`,
      );
    }
    this.drawer().close();
  }

  protected askDelete(row: ReferenceRow): void {
    void this.askDeleteNow(row);
  }

  private async askDeleteNow(row: ReferenceRow): Promise<void> {
    const description = this.description();
    if (description === null) return;
    if (!(await this.confirm().ask())) return;
    const id = this.idOf(row);
    this.actionProblem.set(null);
    this.removing.set(id);
    const result = await this.store.remove(description.controller, id, newIntentKey());
    this.removing.set(null);
    if (result.kind === 'ok') {
      const name = capital(description.singular);
      this.toasts.show($localize`:@@platform-admin.reference.toast.deleted:${name}:NAME: deleted.`);
      return;
    }
    this.actionProblem.set(messageOf(result));
  }
}
