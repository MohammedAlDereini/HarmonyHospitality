import { ChangeDetectionStrategy, Component, signal, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Drawer } from './drawer';
import { Toasts, ToastsHost } from './toasts';

const tick = (ms: number): Promise<void> => new Promise((resolve) => setTimeout(resolve, ms));
const OK_TOAST_MS = 3500;
const A_LITTLE = 50;
const TWICE = 2;

@Component({
  selector: 'hr-host',
  imports: [Drawer, ToastsHost],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hr-drawer
      #drawer
      heading="Add currency"
      lead="The code is fixed once saved."
      (closed)="closes.set(closes() + 1)"
    >
      @if (drawer.isOpen()) {
        <p class="inside">form</p>
      }
    </hr-drawer>
    <hr-toasts />
  `,
})
class Host {
  readonly drawer = viewChild.required(Drawer);
  readonly closes = signal(0);
}

describe('ui/overlays', () => {
  it('the drawer opens with its heading and lead, holds the content, closes by its button and by Escape (close event), and asks the browser for light dismiss', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    const host = fixture.componentInstance;
    const dialog = page.querySelector<HTMLDialogElement>('dialog.drawer');
    if (dialog === null) throw new Error('no drawer');
    expect(dialog.hasAttribute('open')).toBe(false);
    host.drawer().open();
    fixture.detectChanges();
    expect(dialog.hasAttribute('open')).toBe(true);
    expect(page.querySelector('.drawer-h h2')?.textContent).toBe('Add currency');
    expect(dialog.getAttribute('aria-labelledby')).toBe(page.querySelector('.drawer-h h2')?.id);
    expect(page.querySelector('.lead')?.textContent).toBe('The code is fixed once saved.');
    expect(page.querySelector('.inside')?.textContent).toBe('form');
    page.querySelector<HTMLButtonElement>('.drawer-x')?.click();
    fixture.detectChanges();
    expect(dialog.hasAttribute('open')).toBe(false);
    expect(host.closes()).toBe(1);
    expect(page.querySelector('.inside')).toBeNull();

    host.drawer().open();
    dialog.removeAttribute('open');
    dialog.dispatchEvent(new Event('close'));
    fixture.detectChanges();
    expect(host.closes()).toBe(TWICE);

    expect(dialog.getAttribute('closedby')).toBe('any');
  });

  it('a toast shows in the live region and goes after its time; a bad one is marked', async () => {
    vi.useFakeTimers();
    try {
      const fixture = TestBed.createComponent(Host);
      fixture.detectChanges();
      const page = fixture.nativeElement as HTMLElement;
      const toasts = TestBed.inject(Toasts);
      toasts.show('Currency added: JOD.');
      toasts.show('That did not work.', true);
      fixture.detectChanges();
      const shown = [...page.querySelectorAll('.toast')];
      expect(shown.map((toast) => toast.textContent)).toEqual([
        'Currency added: JOD.',
        'That did not work.',
      ]);
      expect(shown[1]?.classList.contains('bad')).toBe(true);
      expect(page.querySelector('[role="status"]')?.getAttribute('aria-live')).toBe('polite');
      vi.advanceTimersByTime(OK_TOAST_MS + A_LITTLE);
      fixture.detectChanges();
      expect([...page.querySelectorAll('.toast')].map((toast) => toast.textContent)).toEqual([
        'That did not work.',
      ]);
    } finally {
      vi.useRealTimers();
    }
    await tick(0);
  });
});
