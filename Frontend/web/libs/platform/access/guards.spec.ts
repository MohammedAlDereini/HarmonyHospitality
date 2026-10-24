import { TestBed } from '@angular/core/testing';
import { PartialMatchRouteSnapshot } from '@angular/router';
import { SessionStore } from '@harmony/platform/session';
import { AccessStore } from './access-store';
import { allowed, moduleEntitled } from './guards';
import { Permission } from './permission';

/** The guards read only the property parameter from the snapshot. */
const at = (property: string): PartialMatchRouteSnapshot =>
  ({ params: { property } }) as unknown as PartialMatchRouteSnapshot;

describe('access guards', () => {
  let reads: number;

  beforeEach(() => {
    reads = 0;
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionStore, useValue: { ready: () => Promise.resolve('signed-in') } },
        {
          provide: AccessStore,
          useValue: {
            ready: () => {
              reads += 1;
              return Promise.resolve();
            },
            can: (permission: Permission) => permission === Permission.ViewRoles,
            entitled: (module: string, property: string) =>
              module === 'front-office' && property === 'GRD',
          },
        },
      ],
    });
  });

  it('allowed(permission): waits for the grants, then matches only for a held permission', async () => {
    const yes = await TestBed.runInInjectionContext(() =>
      allowed(Permission.ViewRoles)({}, [], at('GRD')),
    );
    const no = await TestBed.runInInjectionContext(() =>
      allowed(Permission.ManageUsers)({}, [], at('GRD')),
    );
    expect(yes).toBe(true);
    expect(no).toBe(false);
    expect(reads).toBeGreaterThan(0);
  });

  it('moduleEntitled(module): matches only where the line is held', async () => {
    const yes = await TestBed.runInInjectionContext(() =>
      moduleEntitled('front-office')({}, [], at('GRD')),
    );
    const no = await TestBed.runInInjectionContext(() =>
      moduleEntitled('front-office')({}, [], at('RYD')),
    );
    expect(yes).toBe(true);
    expect(no).toBe(false);
  });
});
