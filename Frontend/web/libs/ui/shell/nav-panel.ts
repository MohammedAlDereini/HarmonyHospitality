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
import { RouterLink, RouterLinkActive } from '@angular/router';
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
 * place by group. A native popover: the browser owns the top layer, Escape and the scrim.
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
  private readonly searchBox = viewChild.required<ElementRef<HTMLInputElement>>('searchBox');
  protected readonly query = signal('');

  /** Groups in first-seen order; the search keeps the items whose label contains the words typed. */
  protected readonly groups = computed(() => {
    const needle = this.query().trim().toLocaleLowerCase();
    const shown = this.items().filter((item) => item.label.toLocaleLowerCase().includes(needle));
    const names = [...new Set(shown.map((item) => item.group))];
    return names.map((name) => ({ name, items: shown.filter((item) => item.group === name) }));
  });

  /** The panel opens: the search box takes focus, as the prototype's panel did. */
  protected onToggle(event: Event): void {
    if ((event as ToggleEvent).newState === 'open') this.searchBox().nativeElement.focus();
  }

  protected search(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  /** Choosing a place closes the panel; the browser returns focus to the button that opened it. */
  protected closeOnLink(event: Event): void {
    if (event.target instanceof Element && event.target.closest('a') !== null)
      this.host.hidePopover();
  }
}
