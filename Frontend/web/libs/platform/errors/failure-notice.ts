import { DOCUMENT, Injectable, inject, signal } from '@angular/core';

export interface Failure {
  /** What the person reads out to support. */
  readonly reference: string;
  /** The sentence to show, already in the person's language. */
  readonly message: string;
}

/** The one notice for an unhandled error: "Something went wrong. Reference 7f3a2c1d" and Reload (architecture 14). */
@Injectable({ providedIn: 'root' })
export class FailureNotice {
  private readonly document = inject(DOCUMENT);
  private readonly failure = signal<Failure | null>(null);

  /** The failure to show, or null. The shell renders it; nothing else reads it. */
  readonly current = this.failure.asReadonly();

  show(failure: Failure): void {
    this.failure.set(failure);
  }

  dismiss(): void {
    this.failure.set(null);
  }

  /** After an unexpected error the state may be corrupted (Angular's guidance): start the page again. */
  reload(): void {
    this.document.defaultView?.location.reload();
  }
}
