import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';

let nextId = 0;

/**
 * The tall panel at the end of the page where a row is added or changed (the platform app's drawer, architecture 8.3
 * overlays): a native modal dialog pinned to the inline-end edge, full height, the page dimmed behind it. The browser
 * owns the top layer, the focus trap, Escape, light dismiss (closedby="any": a click on the scrim) and the return
 * of focus. The page puts the form inside and reads `closed` to drop it.
 *
 *   <hr-drawer #drawer heading="Add currency" lead="…" (closed)="creating.set(false)">
 *     @if (creating()) { <hr-row-editor … /> }
 *   </hr-drawer>
 */
@Component({
  selector: 'hr-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './drawer.html',
  styleUrl: './drawer.css',
})
export class Drawer {
  readonly heading = input.required<string>();
  /** One sentence under the heading: what the form is for, what cannot change. */
  readonly lead = input<string>('');
  /** Fires when the drawer has closed, whatever closed it. */
  readonly closed = output();

  private readonly box = viewChild.required<ElementRef<HTMLDialogElement>>('box');
  private readonly serial = String(++nextId);
  protected readonly headingId = computed(() => `hr-drawer-${this.serial}-heading`);
  readonly isOpen = signal(false);

  open(): void {
    if (this.isOpen()) return;
    const dialog = this.box().nativeElement;
    if (typeof dialog.showModal === 'function') dialog.showModal();
    else dialog.setAttribute('open', '');
    this.isOpen.set(true);
  }

  /**
   * Closes and settles at once. The native close event is not waited for: Chrome drops it after a page lifecycle
   * freeze (seen in the page gate), so the event only serves Escape and the scrim; onClose is guarded for both.
   */
  close(): void {
    if (!this.isOpen()) return;
    const dialog = this.box().nativeElement;
    if (typeof dialog.close === 'function') dialog.close();
    else dialog.removeAttribute('open');
    this.onClose();
  }

  /** The native close (Escape, the scrim) or the programmatic one: settles once. */
  protected onClose(): void {
    if (!this.isOpen()) return;
    this.isOpen.set(false);
    this.closed.emit();
  }
}
