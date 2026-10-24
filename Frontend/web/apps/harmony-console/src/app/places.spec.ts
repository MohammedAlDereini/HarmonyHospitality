import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AccessStore, Permission } from '@harmony/platform/access';
import { SHELL_PLACES } from '@harmony/ui/shell';
import { providePlatformPlaces } from './places';

const PLATFORM_ITEMS = 4;
const REFERENCE_TABLES = 12;
const OWN_ITEMS = 2;

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
    const items = places.items().map((item) => `${item.group}:${item.label}`);
    expect(items.slice(0, PLATFORM_ITEMS)).toEqual([
      'Platform:Home',
      'Platform:Tenants',
      'Platform:Properties',
      'Platform:Reference data',
    ]);
    // Every reference table is one place of its own, between the platform's things and its own people.
    const tables = items.filter((item) => item.startsWith('Reference data:'));
    expect(tables).toHaveLength(REFERENCE_TABLES);
    expect(tables[0]).toBe('Reference data:Currencies');
    expect(tables.at(-1)).toBe('Reference data:Lookup values');
    expect(places.items().find((item) => item.label === 'Cities')?.link).toEqual([
      '/reference',
      'cities',
    ]);
    expect(items.slice(-OWN_ITEMS)).toEqual(['Administration:People', 'Administration:Roles']);
  });
});
