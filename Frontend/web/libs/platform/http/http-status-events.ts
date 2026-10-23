import { Injectable, signal } from '@angular/core';

/** What the session library listens to: a 401 means the session is gone; a 403 asks for one refresh (11.2). */
@Injectable({ providedIn: 'root' })
export class HttpStatusEvents {
  /** Count of 401 answers seen; a change means "the session is gone". */
  readonly unauthenticated = signal(0);
  /** Count of 403 answers seen; a change means "refresh the session once, then show the refusal". */
  readonly forbidden = signal(0);
}
