import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ASCENDING, DESCENDING, SortOrder } from '@harmony/shared/domain';

/** What a column header says to assistive technology about the current sort. */
export type AriaSort = 'ascending' | 'descending' | null;

/** The aria-sort value of one column: set only on the column the list is sorted by. */
export const ariaSort = (member: string, current: SortOrder | null): AriaSort => {
  if (current?.memberName !== member) return null;
  return current.sortOrder === ASCENDING ? 'ascending' : 'descending';
};

/**
 * The button inside a sortable column header: press once to sort by the column ascending, again to flip. The arrow
 * shows the direction; the header's aria-sort says it to a screen reader (set by the page with ariaSort).
 */
@Component({
  selector: 'hr-sort-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="sort" [attr.aria-pressed]="active()" (click)="toggle()">
      <ng-content />
      @if (direction(); as direction) {
        <span class="arrow" aria-hidden="true">{{ direction === up ? '▲' : '▼' }}</span>
      }
    </button>
  `,
})
export class SortButton {
  /** The member name Identity sorts by, e.g. "NameEn". */
  readonly member = input.required<string>();
  readonly current = input.required<SortOrder | null>();
  readonly sorted = output<SortOrder>();

  protected readonly up = ASCENDING;
  protected readonly active = computed(() => this.current()?.memberName === this.member());
  protected readonly direction = computed(() => {
    const current = this.current();
    return current !== null && current.memberName === this.member() ? current.sortOrder : null;
  });

  protected toggle(): void {
    const current = this.current();
    const flip = current !== null && current.memberName === this.member();
    this.sorted.emit({
      memberName: this.member(),
      sortOrder: flip && current.sortOrder === ASCENDING ? DESCENDING : ASCENDING,
    });
  }
}
