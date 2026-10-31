import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReferenceStore, TableCount } from '@harmony/platform-admin/data-access';
import { ReferenceTable } from '@harmony/platform-admin/domain';
import { Icon } from '@harmony/ui/icons';
import { PageHead } from '@harmony/ui/shell';
import { tables } from './tables';

/** A table read per category is never counted as a whole. */
const scopedCount: TableCount = { total: signal('scoped') };

/** One tile of the overview: the table and how many rows it holds. */
interface Tile {
  readonly table: ReferenceTable;
  readonly count: TableCount;
}

/**
 * The reference tables as tiles with their live row counts and what each is for (platform console design, C23): the
 * door to every table; a search narrows the tiles; while any table is still empty a message says the order to fill
 * them in, because each table checks that the rows it points at already exist.
 */
@Component({
  selector: 'hr-reference-index-page',
  imports: [RouterLink, Icon, PageHead],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reference-index-page.html',
  styleUrl: './reference-index-page.css',
})
export class ReferenceIndexPage {
  private readonly store = inject(ReferenceStore);
  protected readonly tiles: readonly Tile[] = tables.map((table) => ({
    table,
    count: table.scopedBy === undefined ? this.store.count(table.controller) : scopedCount,
  }));
  protected readonly search = signal('');
  protected readonly shown = computed<readonly Tile[]>(() => {
    const needle = this.search().trim().toLowerCase();
    if (needle === '') return this.tiles;
    return this.tiles.filter((tile) => tile.table.title.toLowerCase().includes(needle));
  });
  protected readonly allKnown = computed(() =>
    this.tiles.every((tile) => tile.count.total() !== null),
  );
  protected readonly emptyCount = computed(
    () => this.tiles.filter((tile) => tile.count.total() === 0).length,
  );
  /** The rows across every table the person may read. */
  protected readonly totalRows = computed(() =>
    this.tiles.reduce((sum, tile) => {
      const total = tile.count.total();
      return typeof total === 'number' ? sum + total : sum;
    }, 0),
  );

  protected onSearch(value: string): void {
    this.search.set(value);
  }
}
