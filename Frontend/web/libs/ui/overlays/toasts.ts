import { ChangeDetectionStrategy, Component, Injectable, inject, signal } from '@angular/core';

/** One short confirmation at the foot of the screen. */
export interface Toast {
  readonly id: number;
  readonly text: string;
  /** A failure: red, and it stays a little longer. */
  readonly bad: boolean;
}

const OK_MS = 3500;
const BAD_MS = 6000;

/** Short confirmations after a save ("Currency added: JOD."), spoken by any page; the shell shows them. */
@Injectable({ providedIn: 'root' })
export class Toasts {
  private nextId = 0;
  readonly list = signal<readonly Toast[]>([]);

  show(text: string, bad = false): void {
    const id = ++this.nextId;
    this.list.update((toasts) => [...toasts, { id, text, bad }]);
    setTimeout(
      () => {
        this.list.update((toasts) => toasts.filter((toast) => toast.id !== id));
      },
      bad ? BAD_MS : OK_MS,
    );
  }
}

/** Where the toasts appear: once, in the shell, as a polite live region. */
@Component({
  selector: 'hr-toasts',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toasts" role="status" aria-live="polite">
      @for (toast of toasts.list(); track toast.id) {
        <div class="toast" [class.bad]="toast.bad">{{ toast.text }}</div>
      }
    </div>
  `,
  styles: `
    .toasts {
      position: fixed;
      inset-inline-start: 50%;
      inset-block-end: var(--s6);
      translate: -50% 0;
      display: grid;
      gap: var(--s2);
      z-index: 60;
      pointer-events: none;
    }
    html[dir='rtl'] .toasts {
      translate: 50% 0;
    }
    .toast {
      background: var(--shell);
      color: var(--shell-ink);
      padding: var(--s3) var(--s4);
      border-radius: var(--radius-overlay);
      box-shadow: var(--shadow-overlay);
      font-size: 0.92rem;
      max-width: min(32rem, calc(100vw - 2 * var(--s4)));
    }
    .toast.bad {
      background: var(--stop);
      color: var(--on-stop);
    }
  `,
})
export class ToastsHost {
  protected readonly toasts = inject(Toasts);
}
