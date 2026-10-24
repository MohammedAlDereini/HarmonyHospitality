import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReferenceStore, TableCount } from '@harmony/platform-admin/data-access';
import { ReferenceTable } from '@harmony/platform-admin/domain';
import { tables } from './tables';

/** One tile of the overview: the table and how many rows it holds. */
interface Tile {
  readonly table: ReferenceTable;
  readonly count: TableCount;
}

/**
 * The reference tables as tiles with their row counts and what each is for (the platform app's home): a fresh
 * installation is told where to start, because each table checks that the rows it points at already exist.
 */
@Component({
  selector: 'hr-reference-index-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-head">
      <div>
        <h1 i18n="@@platform-admin.reference.title">Reference data</h1>
        <p class="lead" i18n="@@platform-admin.reference.index.lead">
          The lists every hotel in Harmony shares: countries, cities, currencies, time zones,
          languages, property types and the regimes they live under. Kept here by the platform team;
          a hotel only reads them.
        </p>
      </div>
    </header>
    @if (allKnown()) {
      @if (emptyCount() === tiles.length) {
        <p class="tiles-note" i18n="@@platform-admin.reference.index.fresh">
          Every table is empty: this is a fresh installation. Start with currencies and time zones,
          then countries, then the rest; each table checks that the rows it points at already exist.
        </p>
      } @else if (emptyCount() > 0) {
        <p class="tiles-note">
          <span i18n="@@platform-admin.reference.index.still-empty">Still empty:</span>&ngsp;<span
            class="num"
            >{{ emptyCount() }}</span
          >&ngsp;<span i18n="@@platform-admin.reference.index.tables">tables</span>
        </p>
      }
    }
    <div class="tiles">
      @for (tile of tiles; track tile.table.key) {
        <a class="tile" [routerLink]="[tile.table.key]">
          <div class="n" [class.dim]="typeof tile.count.total() !== 'number'">
            @switch (tile.count.total()) {
              @case (null) {
                <ng-container i18n="@@platform-admin.reference.index.counting">…</ng-container>
              }
              @case ('denied') {
                <ng-container i18n="@@platform-admin.reference.index.no-access"
                  >No access</ng-container
                >
              }
              @default {
                <span class="num">{{ tile.count.total() }}</span>
              }
            }
          </div>
          <div class="t">{{ tile.table.title }}</div>
          <div class="d">{{ tile.table.description }}</div>
        </a>
      }
    </div>
  `,
})
export class ReferenceIndexPage {
  private readonly store = inject(ReferenceStore);
  protected readonly tiles: readonly Tile[] = tables.map((table) => ({
    table,
    count: this.store.count(table.controller),
  }));
  protected readonly allKnown = computed(() =>
    this.tiles.every((tile) => tile.count.total() !== null),
  );
  protected readonly emptyCount = computed(
    () => this.tiles.filter((tile) => tile.count.total() === 0).length,
  );
}
