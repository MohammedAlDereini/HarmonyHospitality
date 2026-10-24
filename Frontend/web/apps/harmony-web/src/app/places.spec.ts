import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AccessStore, Permission } from '@harmony/platform/access';
import { CurrentProperty } from '@harmony/platform/context';
import { SHELL_PLACES } from '@harmony/ui/shell';
import { provideHotelPlaces } from './places';

describe('the hotel places', () => {
  const code = signal<string | null>(null);
  /** The grants as the real store keeps them: a signal, so the places follow a change. */
  const granted = signal<ReadonlySet<number>>(new Set());
  const grant = (permission: number): void => {
    granted.set(new Set([...granted(), permission]));
  };

  beforeEach(() => {
    code.set(null);
    granted.set(new Set());
    TestBed.configureTestingModule({
      providers: [
        provideHotelPlaces(),
        { provide: CurrentProperty, useValue: { code } },
        { provide: AccessStore, useValue: { can: (p: number) => granted().has(p) } },
      ],
    });
  });

  it('outside a property only Home; inside, Users and Roles follow the permissions', () => {
    const places = TestBed.inject(SHELL_PLACES);
    expect(places.scope).toBe('property');
    expect(places.dock()).toEqual([]);
    expect(places.items().map((item) => item.label)).toEqual(['Home']);
    code.set('AMM');
    expect(places.dock()).toEqual([]);
    grant(Permission.ViewUsers);
    expect(places.dock().map((place) => place.link.join('/'))).toEqual(['/p/AMM/admin']);
    grant(Permission.ViewRoles);
    expect(places.dock().map((place) => place.link.join('/'))).toEqual([
      '/p/AMM/admin',
      '/p/AMM/admin/roles',
    ]);
    expect(places.items().map((item) => `${item.group}:${item.label}`)).toEqual([
      'Workspaces:Home',
      'Administration:Users',
      'Administration:Roles',
    ]);
  });
});
