import { DestroyRef, Injectable, effect, inject, signal, untracked } from '@angular/core';
import { ApiClient, ApiResult, HttpStatusEvents } from '@harmony/platform/http';
import { firstValueFrom } from 'rxjs';
import { BrowserNavigation } from './browser-navigation';
import { SessionChannel, SessionMessage } from './session-channel';
import { localPathOnly, signInUrl, signedInPath, userPath } from './session-paths';
import { SessionState } from './session-state';
import { SessionUser } from './session-user';

/** After a 403 the session is read again once; further 403s within this window do not read again. */
const FORBIDDEN_REFRESH_WINDOW_MS = 30_000;

/**
 * The one owner of "who is signed in" (architecture 9, 11.2, 14). It reads /bff/user, reacts to 401 and 403 from any
 * call, hears the other tabs, and walks to the sign-in and sign-out doors. It never sees a token.
 */
@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly api = inject(ApiClient);
  private readonly events = inject(HttpStatusEvents);
  private readonly navigation = inject(BrowserNavigation);
  private readonly channel = inject(SessionChannel);
  private readonly stateSignal = signal<SessionState>('unknown');
  private readonly userSignal = signal<SessionUser | null>(null);
  private readonly resumedSignal = signal(0);
  private inFlight: Promise<void> | null = null;
  private first: Promise<SessionState> | null = null;
  private seenUnauthenticated = 0;
  private seenForbidden = 0;
  private lastForbiddenRefreshAt = 0;

  /** unknown until the first read; then signed-in, signed-out or unavailable. */
  readonly state = this.stateSignal.asReadonly();
  /** The last person the BFF named; trust it only while state is signed-in. */
  readonly user = this.userSignal.asReadonly();
  /** Bumps when a separate-window sign-in finished: stores retry their last read. */
  readonly resumed = this.resumedSignal.asReadonly();

  constructor() {
    // A 401 anywhere means the session is gone: the page keeps its state and shows "Sign in again" (11.2).
    effect(() => {
      const count = this.events.unauthenticated();
      untracked(() => {
        if (count > this.seenUnauthenticated) {
          this.seenUnauthenticated = count;
          this.sessionGone();
        }
      });
    });
    // A 403: read the session once more (roles may have changed), then the screen shows the refusal (14).
    effect(() => {
      const count = this.events.forbidden();
      untracked(() => {
        if (count > this.seenForbidden) {
          this.seenForbidden = count;
          this.refreshAfterForbidden();
        }
      });
    });
    inject(DestroyRef).onDestroy(
      this.channel.listen((message) => {
        this.onMessage(message);
      }),
    );
  }

  /** Asks the BFF who is signed in; one read at a time. */
  refresh(): Promise<void> {
    this.inFlight ??= this.read();
    return this.inFlight;
  }

  /** The first read, started once; the sessionReady guard waits for it. */
  ready(): Promise<SessionState> {
    this.first ??= this.firstRead();
    return this.first;
  }

  /** A top-level navigation to the sign-in door; only a path of this app rides along. */
  signIn(returnUrl: string): void {
    this.navigation.go(signInUrl(localPathOnly(returnUrl)));
  }

  /** The session is gone and the page holds work: sign in in a separate window. False when the browser refused it. */
  signInAgain(): boolean {
    return this.navigation.open(signInUrl(signedInPath)) !== null;
  }

  /** Tells the other tabs, then walks to the BFF's sign-out door: a navigation, never a fetch (11.2). */
  signOut(): void {
    const user = this.userSignal();
    if (user === null) return;
    this.channel.post({ type: 'signed-out' });
    this.navigation.go(localPathOnly(user.logoutUrl));
  }

  private async read(): Promise<void> {
    try {
      this.apply(await firstValueFrom(this.api.getPlain<SessionUser>(userPath)));
    } finally {
      this.inFlight = null;
    }
  }

  private async firstRead(): Promise<SessionState> {
    await this.refresh();
    return this.state();
  }

  private apply(result: ApiResult<SessionUser>): void {
    if (result.kind === 'ok') {
      this.userSignal.set(result.data);
      this.stateSignal.set('signed-in');
      return;
    }
    if (result.kind === 'unauthenticated') {
      this.stateSignal.set('signed-out');
      return;
    }
    this.stateSignal.set('unavailable');
  }

  private sessionGone(): void {
    this.stateSignal.set('signed-out');
  }

  private refreshAfterForbidden(): void {
    const now = Date.now();
    if (now - this.lastForbiddenRefreshAt < FORBIDDEN_REFRESH_WINDOW_MS) return;
    this.lastForbiddenRefreshAt = now;
    void this.refresh();
  }

  /** The separate sign-in window finished: read the session again, then let the page resume its last read. */
  private async resume(): Promise<void> {
    await this.refresh();
    if (this.state() === 'signed-in') this.resumedSignal.update((n) => n + 1);
  }

  private onMessage(message: SessionMessage): void {
    const react: Record<SessionMessage['type'], () => void> = {
      'signed-in': () => {
        void this.resume();
      },
      'signed-out': () => {
        this.sessionGone();
      },
    };
    react[message.type]();
  }
}
