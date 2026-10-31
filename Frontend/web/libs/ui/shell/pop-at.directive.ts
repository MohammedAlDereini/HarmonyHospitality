import { DOCUMENT, Directive, ElementRef, inject, input } from '@angular/core';

/** The prototype's menu geometry: 6 px under the button, 12 px from either edge, at least 160 px tall. */
const GAP_BELOW = 6;
const EDGE_MARGIN = 12;
const MIN_HEIGHT = 160;

/**
 * Places a native popover menu under the button that opened it (the prototype's popAt), reading the page's
 * direction so it hangs from the button's start edge, and mirrors the open state onto the button's aria-expanded.
 * The browser does the rest: top layer, Escape, light dismiss, focus back to the button. A window resize while
 * the menu is open places it again, so it keeps hanging from the button.
 */
@Directive({
  selector: '[popover][hrPopAt]',
  host: { '(toggle)': 'onToggle($event)', '(window:resize)': 'onResize()' },
})
export class PopAt {
  /** The button that opens this menu. */
  readonly hrPopAt = input.required<HTMLElement | undefined>();
  private readonly pop = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly document = inject(DOCUMENT);

  protected onToggle(event: Event): void {
    const open = (event as ToggleEvent).newState === 'open';
    const anchor = this.hrPopAt();
    anchor?.setAttribute('aria-expanded', String(open));
    if (open && anchor !== undefined) this.place(anchor);
    if (open) this.focusFirstItem();
  }

  protected onResize(): void {
    const anchor = this.hrPopAt();
    if (anchor !== undefined && this.pop.matches(':popover-open')) this.place(anchor);
  }

  /** The menu button pattern: an open menu holds focus on its first item; Escape or a choice gives it back. */
  private focusFirstItem(): void {
    const first = this.pop.querySelector<HTMLElement>('[role="menuitem"]:not([disabled])');
    if (first === null) return;
    const window = this.document.defaultView;
    if (window === null) return;
    window.requestAnimationFrame(() => {
      first.focus();
    });
  }

  private place(anchor: HTMLElement): void {
    const window = this.document.defaultView;
    if (window === null) return;
    const rect = anchor.getBoundingClientRect();
    const width = this.pop.offsetWidth;
    const top = rect.bottom + GAP_BELOW;
    const start = this.document.documentElement.dir === 'rtl' ? rect.right - width : rect.left;
    const x = Math.max(EDGE_MARGIN, Math.min(start, window.innerWidth - width - EDGE_MARGIN));
    this.pop.style.insetInlineStart = 'auto';
    this.pop.style.left = `${String(x)}px`;
    this.pop.style.top = `${String(top)}px`;
    this.pop.style.maxHeight = `${String(Math.max(MIN_HEIGHT, window.innerHeight - top - EDGE_MARGIN))}px`;
    this.pop.scrollTop = 0;
  }
}
