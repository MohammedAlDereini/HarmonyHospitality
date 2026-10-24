import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ASCENDING, DESCENDING, SortOrder } from '@harmony/shared/domain';
import { Pager } from './pager';
import { SortButton, ariaSort } from './sort-button';

const PAGE_SIZE = 20;
const FORTY_FIVE = 45;
const SECOND_PAGE = 2;
const LAST_PAGE = 3;
// Runs of whitespace, to read a label as one line. Matches "  "; rejects "a".
// Tester: https://regex101.com/?regex=%5Cs%2B&testString=a%20%20b
const whitespace = /\s+/g;

@Component({
  selector: 'hr-host',
  imports: [Pager, SortButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <table>
      <thead>
        <tr>
          <th [attr.aria-sort]="aria('Code')">
            <hr-sort-button member="Code" [current]="sort()" (sorted)="sort.set($event)"
              >Code</hr-sort-button
            >
          </th>
        </tr>
      </thead>
    </table>
    <hr-pager
      [page]="page()"
      [pageSize]="size"
      [total]="total()"
      [loading]="loading()"
      (pageChange)="page.set($event)"
    />
  `,
})
class Host {
  readonly sort = signal<SortOrder | null>(null);
  readonly page = signal(1);
  readonly size = PAGE_SIZE;
  readonly total = signal(FORTY_FIVE);
  readonly loading = signal(false);
  aria(member: string): string | null {
    return ariaSort(member, this.sort());
  }
}

describe('ui/data', () => {
  const make = (): { page: HTMLElement; host: Host; render: () => void } => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return {
      page: fixture.nativeElement as HTMLElement,
      host: fixture.componentInstance,
      render: () => {
        fixture.detectChanges();
      },
    };
  };

  it('the sort button sorts ascending first, flips on the second press, and the header says so', () => {
    const { page, host, render } = make();
    const button = page.querySelector<HTMLButtonElement>('th .sort');
    expect(page.querySelector('th')?.getAttribute('aria-sort')).toBeNull();
    button?.click();
    render();
    expect(host.sort()).toEqual({ memberName: 'Code', sortOrder: ASCENDING });
    expect(page.querySelector('th')?.getAttribute('aria-sort')).toBe('ascending');
    expect(page.querySelector('.arrow')?.textContent).toBe('▲');
    button?.click();
    render();
    expect(host.sort()).toEqual({ memberName: 'Code', sortOrder: DESCENDING });
    expect(page.querySelector('th')?.getAttribute('aria-sort')).toBe('descending');
  });

  it('the pager counts the pages, walks them and stops at the ends', () => {
    const { page, host, render } = make();
    const text = (): string =>
      page.querySelector('.where')?.textContent.replace(whitespace, ' ') ?? '';
    expect(text()).toContain('Page 1 of 3');
    const [previous, next] = [...page.querySelectorAll<HTMLButtonElement>('hr-pager .btn')];
    expect(previous?.disabled).toBe(true);
    next?.click();
    render();
    expect(host.page()).toBe(SECOND_PAGE);
    next?.click();
    render();
    expect(host.page()).toBe(LAST_PAGE);
    expect(next?.disabled).toBe(true);
    expect(text()).toContain('Page 3 of 3');
    host.loading.set(true);
    render();
    expect(page.querySelector('.count')?.textContent).toContain('Loading');
    expect(previous?.disabled).toBe(true);
  });
});
