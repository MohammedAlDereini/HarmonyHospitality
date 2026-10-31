import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

const FIRST_PAGE = 1;
const ONE_PAGE = 1;

/**
 * The pager under a table: how many rows in all, Previous, "Page 2 of 5", Next. Pages are 1-based, as Identity counts
 * them; the page asks for the next one and reads it again.
 */
@Component({
  selector: 'hr-pager',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'pager' },
  template: `
    <span class="count" role="status">
      @if (loading()) {
        <ng-container i18n="@@ui.data.pager.loading">Loading…</ng-container>
      } @else {
        <span class="num">{{ total() }}</span
        >&ngsp;<ng-container i18n="@@ui.data.pager.rows">{total(), plural,
          =1 {row}
          other {rows}
        }</ng-container>
      }
    </span>
    <span class="spacer"></span>
    <button
      type="button"
      class="btn"
      (click)="go(page() - step)"
      [disabled]="!hasPrevious() || loading()"
      i18n="@@ui.data.pager.previous"
    >
      Previous
    </button>
    <span class="where"
      ><ng-container i18n="@@ui.data.pager.page">Page</ng-container>&ngsp;<span class="num">{{
        page()
      }}</span
      >&ngsp;<ng-container i18n="@@ui.data.pager.of">of</ng-container>&ngsp;<span class="num">{{
        pages()
      }}</span></span
    >
    <button
      type="button"
      class="btn"
      (click)="go(page() + step)"
      [disabled]="!hasNext() || loading()"
      i18n="@@ui.data.pager.next"
    >
      Next
    </button>
  `,
})
export class Pager {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly loading = input<boolean>(false);
  readonly pageChange = output<number>();

  protected readonly step = ONE_PAGE;
  protected readonly pages = computed(() =>
    Math.max(FIRST_PAGE, Math.ceil(this.total() / this.pageSize())),
  );
  protected readonly hasPrevious = computed(() => this.page() > FIRST_PAGE);
  protected readonly hasNext = computed(() => this.page() < this.pages());

  protected go(page: number): void {
    if (page < FIRST_PAGE || page > this.pages()) return;
    this.pageChange.emit(page);
  }
}
