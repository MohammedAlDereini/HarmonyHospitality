import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PartialMatchRouteSnapshot } from '@angular/router';
import { SessionStore, SessionUser } from '@harmony/platform/session';
import { ACCESS_GRANTS, AccessGrants } from './access-grants';
import { allowed, moduleEntitled } from './guards';
import { Permission } from './permission';

const ahmad: SessionUser = {
  sub: 'u1',
  name: 'Ahmad',
  tenant: 't1',
  roles: [],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=1',
};

const grants: AccessGrants = {
  permissions: new Set<Permission>([Permission.ViewRoles]),
  modules: { GRD: ['front-office'] },
};

/** The guards read only the property parameter from the snapshot. */
const at = (property: string): PartialMatchRouteSnapshot =>
  ({ params: { property } }) as unknown as PartialMatchRouteSnapshot;

describe('access guards', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: SessionStore,
          useValue: { user: signal(ahmad), ready: () => Promise.resolve('signed-in') },
        },
        { provide: ACCESS_GRANTS, useValue: () => grants },
      ],
    });
  });

  it('allowed(permission): matches only for a held permission', async () => {
    const yes = await TestBed.runInInjectionContext(() =>
      allowed(Permission.ViewRoles)({}, [], at('GRD')),
    );
    const no = await TestBed.runInInjectionContext(() =>
      allowed(Permission.ManageUsers)({}, [], at('GRD')),
    );
    expect(yes).toBe(true);
    expect(no).toBe(false);
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
