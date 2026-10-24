import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SessionStore, SessionUser } from '@harmony/platform/session';
import { ACCESS_GRANTS, AccessGrants } from './access-grants';
import { AccessStore } from './access-store';
import { Permission } from './permission';

const ahmad: SessionUser = {
  sub: 'u1',
  name: 'Ahmad',
  tenant: 't1',
  roles: ['FrontDesk'],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=1',
};

const grants: AccessGrants = {
  permissions: new Set<Permission>([Permission.ViewUsers]),
  modules: { GRD: ['front-office'], '*': ['reports'] },
};

const configure = (
  user: SessionUser | null,
  source: (user: SessionUser) => AccessGrants | null,
) => {
  TestBed.configureTestingModule({
    providers: [
      { provide: SessionStore, useValue: { user: signal(user) } },
      { provide: ACCESS_GRANTS, useValue: source },
    ],
  });
  return TestBed.inject(AccessStore);
};

describe('AccessStore', () => {
  it('nobody signed in: nothing is known, nothing is allowed', () => {
    const access = configure(null, () => grants);
    expect(access.known()).toBe(false);
    expect(access.can(Permission.ViewUsers)).toBe(false);
    expect(access.entitled('front-office', 'GRD')).toBe(false);
  });

  it('the default source answers unknown until the session payload exists (D2)', () => {
    const access = TestBed.configureTestingModule({
      providers: [{ provide: SessionStore, useValue: { user: signal(ahmad) } }],
    }).inject(AccessStore);
    expect(access.known()).toBe(false);
    expect(access.can(Permission.ViewUsers)).toBe(false);
  });

  it('with grants: a held permission is allowed, another is not', () => {
    const access = configure(ahmad, () => grants);
    expect(access.known()).toBe(true);
    expect(access.can(Permission.ViewUsers)).toBe(true);
    expect(access.can(Permission.ManageUsers)).toBe(false);
  });

  it('modules: held for the property, or chain-wide, or not at all', () => {
    const access = configure(ahmad, () => grants);
    expect(access.entitled('front-office', 'GRD')).toBe(true);
    expect(access.entitled('front-office', 'RYD')).toBe(false);
    expect(access.entitled('reports', 'RYD')).toBe(true);
  });
});
