import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AccessStore, Permission } from '@harmony/platform/access';
import { CONSOLE_PLACES, ConsoleLink, ConsolePlaces } from '@harmony/ui/console-shell';
import { providePlatformPlaces } from './places';

const PLATFORM_ITEMS = 5;
const REFERENCE_TABLES = 13;
const FOOT_ITEMS = 3;

const linksOf = (places: readonly ConsoleLink[]): string[] =>
  places.map((place) => place.link.join('/'));
/** The links of one section of the rail, by its position; an absent section is an empty list. */
const sectionLinks = (places: ConsolePlaces, index: number): string[] =>
  linksOf(places.sections()[index]?.items ?? []);
const headings = (places: ConsolePlaces): string[] =>
  places.sections().map((section) => section.heading);

describe('the platform console places', () => {
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

  it('shows only Home to a person with no grants, and no foot or operations', () => {
    const places = TestBed.inject(CONSOLE_PLACES);
    expect(headings(places)).toEqual(['Platform']);
    expect(sectionLinks(places, 0)).toEqual(['/']);
    expect(places.sections()[0]?.items[0]?.exact).toBe(true);
    expect(places.bottom()).toEqual([]);
    expect(linksOf(places.phone())).toEqual(['/']);
    expect(places.items().map((item) => item.label)).toEqual(['Home']);
  });

  it('opens each section as its permission arrives, in the Figma order', () => {
    const places = TestBed.inject(CONSOLE_PLACES);
    grant(Permission.ViewUsers);
    expect(headings(places)).toEqual(['Platform', 'Operations', 'Administration']);
    expect(sectionLinks(places, 1)).toEqual(['/sign-ins']);
    expect(sectionLinks(places, 2)).toEqual(['/people']);
    grant(Permission.ViewTenants, Permission.ViewRoles);
    expect(sectionLinks(places, 0)).toEqual([
      '/',
      '/tenants',
      '/properties',
      '/groups',
      '/reference',
    ]);
    expect(sectionLinks(places, 1)).toEqual(['/trail', '/sign-ins']);
    expect(sectionLinks(places, 2)).toEqual(['/people', '/roles']);
    grant(Permission.ManageTenants, Permission.ViewSettings);
    expect(sectionLinks(places, 1)).toEqual(['/onboarding', '/trail', '/sign-ins', '/health']);
    expect(linksOf(places.bottom())).toEqual(['/settings']);
  });

  it('keeps the phone bar to home, tenants, health and the trail, in that order', () => {
    const places = TestBed.inject(CONSOLE_PLACES);
    grant(Permission.ViewTenants);
    expect(linksOf(places.phone())).toEqual(['/', '/tenants', '/trail']);
    grant(Permission.ViewSettings);
    expect(linksOf(places.phone())).toEqual(['/', '/tenants', '/health', '/trail']);
  });

  it('lists every place for the palette, each reference table as a place of its own', () => {
    const places = TestBed.inject(CONSOLE_PLACES);
    grant(
      Permission.ViewTenants,
      Permission.ManageTenants,
      Permission.ViewUsers,
      Permission.ViewRoles,
      Permission.ViewSettings,
    );
    const items = places.items().map((item) => `${item.group}:${item.label}`);
    expect(items.slice(0, PLATFORM_ITEMS)).toEqual([
      'Platform:Home',
      'Platform:Tenants',
      'Platform:Properties',
      'Platform:Groups',
      'Platform:Reference data',
    ]);
    const tables = items.filter((item) => item.startsWith('Reference data:'));
    expect(tables).toHaveLength(REFERENCE_TABLES);
    expect(tables[0]).toBe('Reference data:Currencies');
    expect(tables.at(-1)).toBe('Reference data:Settings registry');
    expect(places.items().find((item) => item.label === 'Cities')?.link).toEqual([
      '/reference',
      'cities',
    ]);
    expect(items.slice(PLATFORM_ITEMS + REFERENCE_TABLES, -FOOT_ITEMS)).toEqual([
      'Operations:Onboarding',
      'Operations:Audit log',
      'Operations:Sign-ins',
      'Operations:Health',
    ]);
    expect(items.slice(-FOOT_ITEMS)).toEqual([
      'Administration:People',
      'Administration:Roles',
      'Administration:Settings',
    ]);
  });
});
