import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_RETRY_DELAYS, HttpStatusEvents, provideHarmonyHttp } from '@harmony/platform/http';
import { BrowserNavigation } from './browser-navigation';
import { SessionChannel, SessionMessage } from './session-channel';
import { SessionStore } from './session-store';
import { SessionUser } from './session-user';

const UNAUTHORIZED = 401;
const SERVICE_UNAVAILABLE = 503;
const ATTEMPTS = 3;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const ahmad: SessionUser = {
  sub: 'u1',
  name: 'Ahmad',
  tenant: 't1',
  roles: ['FrontDesk'],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=abc',
};

class FakeNavigation {
  went: string[] = [];
  opened: string[] = [];
  blocked = false;
  go(path: string): void {
    this.went.push(path);
  }
  open(path: string): Window | null {
    this.opened.push(path);
    return this.blocked ? null : window;
  }
}

class FakeChannel {
  posted: SessionMessage[] = [];
  private handlers: ((message: SessionMessage) => void)[] = [];
  post(message: SessionMessage): void {
    this.posted.push(message);
  }
  listen(handler: (message: SessionMessage) => void): () => void {
    this.handlers.push(handler);
    return () => undefined;
  }
  /** What another tab said. */
  receive(message: SessionMessage): void {
    this.handlers.forEach((handler) => {
      handler(message);
    });
  }
}

/** The BFF not answering, `times` in a row, letting the retry timer run between them. */
const failTimes = (http: HttpTestingController, times: number): Promise<void> =>
  Array.from({ length: times }).reduce<Promise<void>>(async (previous) => {
    await previous;
    http.expectOne('/bff/user').flush(null, { status: SERVICE_UNAVAILABLE, statusText: 'Down' });
    await tick();
  }, Promise.resolve());

describe('SessionStore', () => {
  let store: SessionStore;
  let http: HttpTestingController;
  let events: HttpStatusEvents;
  let navigation: FakeNavigation;
  let channel: FakeChannel;

  const signedInAsAhmad = async (): Promise<void> => {
    const ready = store.ready();
    http.expectOne('/bff/user').flush(ahmad);
    await ready;
  };

  beforeEach(() => {
    navigation = new FakeNavigation();
    channel = new FakeChannel();
    TestBed.configureTestingModule({
      providers: [
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
        { provide: BrowserNavigation, useValue: navigation },
        { provide: SessionChannel, useValue: channel },
      ],
    });
    store = TestBed.inject(SessionStore);
    http = TestBed.inject(HttpTestingController);
    events = TestBed.inject(HttpStatusEvents);
  });

  afterEach(() => {
    http.verify();
  });

  it('starts unknown; the first read names the person and the session is signed-in', async () => {
    expect(store.state()).toBe('unknown');
    const ready = store.ready();
    const request = http.expectOne('/bff/user');
    expect(request.request.headers.get('x-csrf')).toBe('1');
    request.flush(ahmad);
    expect(await ready).toBe('signed-in');
    expect(store.user()).toEqual(ahmad);
  });

  it('a 401 on the first read: signed-out, nobody named, no navigation by itself', async () => {
    const ready = store.ready();
    http.expectOne('/bff/user').flush(null, { status: UNAUTHORIZED, statusText: 'No session' });
    expect(await ready).toBe('signed-out');
    expect(store.user()).toBeNull();
    expect(navigation.went).toEqual([]);
  });

  it('the BFF not answering: unavailable after the retries', async () => {
    const ready = store.ready();
    await failTimes(http, ATTEMPTS);
    expect(await ready).toBe('unavailable');
  });

  it('a 401 from any call marks the session gone; the page and the person stay', async () => {
    await signedInAsAhmad();
    events.unauthenticated.update((n) => n + 1);
    TestBed.tick();
    expect(store.state()).toBe('signed-out');
    expect(store.user()).toEqual(ahmad);
    expect(navigation.went).toEqual([]);
  });

  it('a 403 reads the session once more; a second 403 soon after does not', async () => {
    await signedInAsAhmad();
    events.forbidden.update((n) => n + 1);
    TestBed.tick();
    http.expectOne('/bff/user').flush(ahmad);
    await tick();
    events.forbidden.update((n) => n + 1);
    TestBed.tick();
    http.expectNone('/bff/user');
  });

  it('sign-in is a top-level navigation carrying only a local return path', () => {
    store.signIn('/p/GRD/front-office?tab=arrivals');
    store.signIn('https://evil.example/');
    expect(navigation.went).toEqual([
      '/bff/login?returnUrl=%2Fp%2FGRD%2Ffront-office%3Ftab%3Darrivals',
      '/bff/login?returnUrl=%2F',
    ]);
  });

  it('sign in again opens a separate window landing on /signed-in; false when the browser refuses', () => {
    expect(store.signInAgain()).toBe(true);
    expect(navigation.opened).toEqual(['/bff/login?returnUrl=%2Fsigned-in']);
    navigation.blocked = true;
    expect(store.signInAgain()).toBe(false);
  });

  it('sign-out tells the other tabs, then walks to the sign-out door with the session sid', async () => {
    store.signOut();
    expect(navigation.went).toEqual([]);
    await signedInAsAhmad();
    store.signOut();
    expect(channel.posted).toEqual([{ type: 'signed-out' }]);
    expect(navigation.went).toEqual(['/bff/logout?sid=abc']);
  });

  it('another tab signed out: this tab is signed out too, its work kept', async () => {
    await signedInAsAhmad();
    channel.receive({ type: 'signed-out' });
    expect(store.state()).toBe('signed-out');
    expect(navigation.went).toEqual([]);
  });

  it('the separate window finished: the session is read again and the page resumes', async () => {
    await signedInAsAhmad();
    channel.receive({ type: 'signed-out' });
    channel.receive({ type: 'signed-in' });
    http.expectOne('/bff/user').flush(ahmad);
    await tick();
    expect(store.state()).toBe('signed-in');
    expect(store.resumed()).toBe(1);
  });
});
