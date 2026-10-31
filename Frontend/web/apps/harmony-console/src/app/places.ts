import { Provider, computed, inject } from '@angular/core';
import { AccessStore, Permission } from '@harmony/platform/access';
import { referenceCatalogue } from '@harmony/platform-admin/domain';
import {
  CONSOLE_PLACES,
  ConsoleLink,
  ConsolePlaces,
  ConsoleSection,
} from '@harmony/ui/console-shell';
import { NavItem } from '@harmony/ui/shell';

/** One place of the platform console and the permission that shows it (presentation only; the API refuses on its own). */
interface PlatformPlace extends ConsoleLink {
  readonly permission: Permission | null;
}

/**
 * The platform console's places, in the Figma's three sections (Side nav 4:68): the platform's own things, the
 * operations around them, then the platform tenant's own people and roles; Settings at the foot.
 */
const platformPlaces = (): ConsolePlaces => {
  const access = inject(AccessStore);
  const platform = $localize`:@@ui.shell.nav.group.platform:Platform`;
  const operations = $localize`:@@ui.console.nav.group.operations:Operations`;
  const administration = $localize`:@@ui.shell.nav.group.administration:Administration`;
  const reference = $localize`:@@ui.shell.nav.group.reference:Reference data`;
  const platformOnes: readonly PlatformPlace[] = [
    {
      label: $localize`:@@ui.shell.nav.home:Home`,
      link: ['/'],
      icon: 'house',
      exact: true,
      permission: null,
    },
    {
      label: $localize`:@@ui.shell.nav.tenants:Tenants`,
      link: ['/tenants'],
      icon: 'tenants',
      permission: Permission.ViewTenants,
    },
    {
      label: $localize`:@@ui.shell.nav.properties:Properties`,
      link: ['/properties'],
      icon: 'properties',
      permission: Permission.ViewTenants,
    },
    {
      label: $localize`:@@ui.shell.nav.groups:Groups`,
      link: ['/groups'],
      icon: 'groups',
      permission: Permission.ViewTenants,
    },
    {
      label: $localize`:@@ui.shell.nav.reference:Reference data`,
      link: ['/reference'],
      icon: 'reference',
      permission: Permission.ViewTenants,
    },
  ];
  const operationOnes: readonly PlatformPlace[] = [
    {
      label: $localize`:@@ui.console.nav.onboarding:Onboarding`,
      link: ['/onboarding'],
      icon: 'onboarding',
      permission: Permission.ManageTenants,
    },
    {
      label: $localize`:@@ui.console.nav.trail:Audit log`,
      link: ['/trail'],
      icon: 'audit',
      permission: Permission.ViewTenants,
    },
    {
      label: $localize`:@@ui.console.nav.sign-ins:Sign-ins`,
      link: ['/sign-ins'],
      icon: 'signins',
      permission: Permission.ViewUsers,
    },
    {
      label: $localize`:@@ui.console.nav.health:Health`,
      link: ['/health'],
      icon: 'health',
      permission: Permission.ViewSettings,
    },
  ];
  const ownOnes: readonly PlatformPlace[] = [
    {
      label: $localize`:@@ui.shell.nav.people:People`,
      link: ['/people'],
      icon: 'people',
      permission: Permission.ViewUsers,
    },
    {
      label: $localize`:@@ui.shell.nav.roles:Roles`,
      link: ['/roles'],
      icon: 'roles',
      permission: Permission.ViewRoles,
    },
  ];
  const footOnes: readonly PlatformPlace[] = [
    {
      label: $localize`:@@ui.console.nav.settings:Settings`,
      link: ['/settings'],
      icon: 'settings',
      permission: Permission.ViewSettings,
    },
  ];
  const asLink = (place: PlatformPlace): ConsoleLink =>
    place.exact === true
      ? { label: place.label, link: place.link, icon: place.icon, exact: true }
      : { label: place.label, link: place.link, icon: place.icon };
  const allowed = (places: readonly PlatformPlace[]): ConsoleLink[] =>
    places.filter((place) => place.permission === null || access.can(place.permission)).map(asLink);
  const sections = computed<readonly ConsoleSection[]>(() =>
    [
      { heading: platform, items: allowed(platformOnes) },
      { heading: operations, items: allowed(operationOnes) },
      { heading: administration, items: allowed(ownOnes) },
    ].filter((section) => section.items.length > 0),
  );
  const bottom = computed<readonly ConsoleLink[]>(() => allowed(footOnes));
  // The phone keeps what an on-call person opens: home, the customers, health and the trail.
  const phoneLinks: readonly string[] = ['/', '/tenants', '/health', '/trail'];
  const phone = computed<readonly ConsoleLink[]>(() => {
    const shown = [...allowed(platformOnes), ...allowed(operationOnes)];
    return phoneLinks
      .map((link) => shown.find((place) => place.link.join('/') === link))
      .filter((place): place is ConsoleLink => place !== undefined);
  });
  // Every reference table is one click away in the palette, as the platform app's rail had them.
  const referenceOnes = computed<readonly NavItem[]>(() =>
    access.can(Permission.ViewTenants)
      ? referenceCatalogue.map((table) => ({
          label: table.title,
          link: ['/reference', table.key],
          icon: 'reference' as const,
          group: reference,
        }))
      : [],
  );
  const items = computed<readonly NavItem[]>(() => [
    ...allowed(platformOnes).map((place) => ({ ...place, group: platform })),
    ...referenceOnes(),
    ...allowed(operationOnes).map((place) => ({ ...place, group: operations })),
    ...allowed(ownOnes).map((place) => ({ ...place, group: administration })),
    ...allowed(footOnes).map((place) => ({ ...place, group: administration })),
  ]);
  return { sections, bottom, phone, items };
};

export const providePlatformPlaces = (): Provider => ({
  provide: CONSOLE_PLACES,
  useFactory: platformPlaces,
});
