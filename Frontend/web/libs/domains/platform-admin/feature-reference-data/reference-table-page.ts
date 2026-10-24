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
import { ReferenceStore, TableRows } from '@harmony/platform-admin/data-access';
import { ReferenceRow, ReferenceTable } from '@harmony/platform-admin/domain';
import { AccessStore, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { Icon } from '@harmony/ui/icons';
import { PageActionsTemplate } from '@harmony/ui/shell';
import { RowEditor } from './row-editor';
import { tableByKey } from './tables';

/** What is open under a row. */
type Opened = 'edit' | 'delete';

interface Editing {
  readonly id: string;
  readonly kind: Opened;
}

/**
 * One reference table as rows, from its description: the columns, a new row from the context bar's action slot,
 * edit under the row, delete with a confirmation where the table allows it (architecture 6, platform-admin). Lookup
 * values are read for one category at a time.
 */
@Component({
  selector: 'hr-reference-table-page',
  imports: [Icon, PageActionsTemplate, RowEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reference-table-page.html',
  styleUrl: './reference-table-page.css',
})
export class ReferenceTablePage {
  /** From the route (:table), bound by the router. */
  readonly table = input.required<string>();

  private readonly store = inject(ReferenceStore);
  private readonly access = inject(AccessStore);

  protected readonly description = computed<ReferenceTable | null>(() => tableByKey(this.table()));
  protected readonly rows = computed<TableRows | null>(() => {
    const description = this.description();
    return description === null ? null : this.store.table(description.controller);
  });
  /** Lookup values: the categories to choose from. */
  protected readonly categories = this.store.table('LookupCategory');
  protected readonly category = signal('');
  protected readonly mayManage = computed(() => {
    const description = this.description();
    if (description === null) return false;
    return this.access.can(
      description.lookup === true ? Permission.ManageLookups : Permission.ManageTenants,
    );
  });
  protected readonly creating = signal(false);
  protected readonly editing = signal<Editing | null>(null);
  protected readonly removing = signal<string | null>(null);
  protected readonly actionProblem = signal<string | null>(null);

  constructor() {
    effect(() => {
      const description = this.description();
      const rows = this.rows();
      untracked(() => {
        this.creating.set(false);
        this.editing.set(null);
        this.category.set('');
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
    return description === null ? '' : String(row[description.idField] ?? '');
  }

  protected cell(row: ReferenceRow, column: string): string {
    const value = row[column];
    if (value === true) return $localize`:@@platform-admin.reference.yes:Yes`;
    if (value === false) return $localize`:@@platform-admin.reference.no:No`;
    return value === null || value === undefined ? '' : String(value);
  }

  protected columnLabel(column: string): string {
    const description = this.description();
    return description?.fields.find((field) => field.key === column)?.label ?? column;
  }

  protected chooseCategory(id: string): void {
    this.category.set(id);
    const description = this.description();
    if (description?.scopedBy !== undefined && id !== '') {
      this.rows()?.load({ [description.scopedBy]: id });
    }
  }

  protected opened(row: ReferenceRow): Opened | null {
    const editing = this.editing();
    return editing !== null && editing.id === this.idOf(row) ? editing.kind : null;
  }

  protected openCreator(): void {
    this.actionProblem.set(null);
    this.editing.set(null);
    this.creating.set(true);
  }

  protected closeCreator(): void {
    this.creating.set(false);
  }

  protected open(row: ReferenceRow, kind: Opened): void {
    this.actionProblem.set(null);
    this.creating.set(false);
    this.editing.set({ id: this.idOf(row), kind });
  }

  protected close(): void {
    this.editing.set(null);
  }

  protected remove(row: ReferenceRow): void {
    void this.removeNow(row);
  }

  private async removeNow(row: ReferenceRow): Promise<void> {
    const description = this.description();
    if (description === null) return;
    const id = this.idOf(row);
    this.actionProblem.set(null);
    this.removing.set(id);
    const result = await this.store.remove(description.controller, id, newIntentKey());
    this.removing.set(null);
    if (result.kind === 'ok') {
      this.close();
      return;
    }
    this.actionProblem.set(messageOf(result));
  }
}
