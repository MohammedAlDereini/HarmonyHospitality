import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AccessStore, Permission } from '@harmony/platform/access';
import { SHELL_PLACES } from '@harmony/ui/shell';
import { providePlatformPlaces } from './places';

describe('the platform places', () => {
  /** The grants as the real store keeps them: a signal, so the places follow a change. */
  const granted = signal<ReadonlySet<number>>(new Set());
  const grant = (...permissions: number[]): void => {
    granted.set(new Set([...granted(), ...permissions]));
  };

  beforeEach(() => {
    granted.set(new Set());
    TestBed.configureTestingModule({
      providers: [
        providePlatformPlaces(),
        { provide: AccessStore, useValue: { can: (p: number) => granted().has(p) } },
      ],
    });
  });

  it('never a property scope; each place follows its permission; the platform group comes first', () => {
    const places = TestBed.inject(SHELL_PLACES);
    expect(places.scope).toBe('platform');
    expect(places.dock()).toEqual([]);
    expect(places.items().map((item) => item.label)).toEqual(['Home']);
    grant(Permission.ViewUsers);
    expect(places.dock().map((place) => place.link.join('/'))).toEqual(['/people']);
    grant(Permission.ViewTenants, Permission.ViewRoles);
    expect(places.dock().map((place) => place.link.join('/'))).toEqual([
      '/tenants',
      '/properties',
      '/reference',
      '/people',
      '/roles',
    ]);
    expect(places.items().map((item) => `${item.group}:${item.label}`)).toEqual([
      'Platform:Home',
      'Platform:Tenants',
      'Platform:Properties',
      'Platform:Reference data',
      'Administration:People',
      'Administration:Roles',
    ]);
  });
});
