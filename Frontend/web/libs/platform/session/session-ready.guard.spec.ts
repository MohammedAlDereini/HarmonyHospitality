import { TestBed } from '@angular/core/testing';
import { PartialMatchRouteSnapshot, provideRouter } from '@angular/router';
import { sessionReady } from './session-ready.guard';
import { SessionState } from './session-state';
import { SessionStore } from './session-store';

/** The guard reads nothing from its route, segments or snapshot; an empty snapshot is enough. */
const noSnapshot = {} as PartialMatchRouteSnapshot;

describe('sessionReady', () => {
  const signedIn: string[] = [];
  let state: SessionState;

  const run = (): Promise<unknown> =>
    Promise.resolve(TestBed.runInInjectionContext(() => sessionReady({}, [], noSnapshot)));

  beforeEach(() => {
    signedIn.length = 0;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: SessionStore,
          useValue: {
            ready: () => Promise.resolve(state),
            signIn: (returnUrl: string) => {
              signedIn.push(returnUrl);
            },
          },
        },
      ],
    });
  });

  it('signed-in: the route matches', async () => {
    state = 'signed-in';
    expect(await run()).toBe(true);
    expect(signedIn).toEqual([]);
  });

  it('signed-out: walks to the sign-in door and the route does not match', async () => {
    state = 'signed-out';
    expect(await run()).toBe(false);
    expect(signedIn).toEqual(['/']);
  });

  it('expired: the sign-in door as well', async () => {
    state = 'expired';
    expect(await run()).toBe(false);
    expect(signedIn).toEqual(['/']);
  });

  it('unavailable: nothing matches and nobody is sent anywhere', async () => {
    state = 'unavailable';
    expect(await run()).toBe(false);
    expect(signedIn).toEqual([]);
  });
});
