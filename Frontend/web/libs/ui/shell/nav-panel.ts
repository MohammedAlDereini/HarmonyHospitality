import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Icon, IconName } from '@harmony/ui/icons';

/** One place the person can go, in words; the dock shows the same places as icons. */
export interface NavItem {
  readonly label: string;
  readonly link: readonly string[];
  readonly icon: IconName;
  /** The group heading it sits under. */
  readonly group: string;
  readonly exact?: true;
}

/**
 * The labelled panel the prototype opens from the dock's foot and the bar's menu button: a search box, then every
 * place by group. A native popover: the browser owns the top layer, Escape and the scrim. It is also the command
 * palette (architecture 8.3, WCAG 2.4.5): Ctrl K opens it with the search focused, Enter goes to the first match,
 * ArrowDown walks the results; the places listed are only those the person's grants allow.
 */
@Component({
  selector: 'hr-nav-panel',
  imports: [RouterLink, RouterLinkActive, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'navp', '(click)': 'closeOnLink($event)', '(toggle)': 'onToggle($event)' },
  templateUrl: './nav-panel.html',
  styleUrl: './nav-panel.css',
})
export class NavPanel {
  readonly items = input.required<readonly NavItem[]>();
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly router = inject(Router);
  private opened = false;
  private readonly searchBox = viewChild.required<ElementRef<HTMLInputElement>>('searchBox');
  protected readonly query = signal('');

  /** Groups in first-seen order; the search keeps the items whose label contains the words typed. */
  protected readonly groups = computed(() => {
    const needle = this.query().trim().toLocaleLowerCase();
    const shown = this.items().filter((item) => item.label.toLocaleLowerCase().includes(needle));
    const names = [...new Set(shown.map((item) => item.group))];
    return names.map((name) => ({ name, items: shown.filter((item) => item.group === name) }));
  });

  /** Opens the panel (Ctrl K) with the search box focused; already open, it only refocuses the search. */
  open(): void {
    if (!this.opened && typeof this.host.showPopover === 'function') this.host.showPopover();
    this.searchBox().nativeElement.focus();
  }

  /** The panel opens: the search box takes focus, as the prototype's panel did. */
  protected onToggle(event: Event): void {
    this.opened = (event as ToggleEvent).newState === 'open';
    if (this.opened) this.searchBox().nativeElement.focus();
  }

  /**
   * Enter goes to the first place shown; ArrowDown moves into the results; Escape clears a search, and closes the
   * panel when there is nothing to clear (a search box swallows Escape itself, so the popover would not see it).
   */
  protected onSearchKey(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      if (this.query() !== '') {
        this.query.set('');
        this.searchBox().nativeElement.value = '';
      } else {
        this.close();
      }
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.host.querySelector<HTMLElement>('a.ni')?.focus();
      return;
    }
    if (event.key !== 'Enter') return;
    const first = this.groups()[0]?.items[0];
    if (first === undefined) return;
    event.preventDefault();
    void this.router.navigate([...first.link]);
    this.close();
  }

  protected search(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  /** Choosing a place closes the panel; the browser returns focus to the button that opened it. */
  protected closeOnLink(event: Event): void {
    if (event.target instanceof Element && event.target.closest('a') !== null) this.close();
  }

  private close(): void {
    if (this.opened && typeof this.host.hidePopover === 'function') this.host.hidePopover();
  }
}
