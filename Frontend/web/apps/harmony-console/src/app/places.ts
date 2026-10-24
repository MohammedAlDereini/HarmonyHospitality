import { Provider, computed, inject } from '@angular/core';
import { AccessStore, Permission } from '@harmony/platform/access';
import { DockLink, NavItem, SHELL_PLACES, ShellPlaces } from '@harmony/ui/shell';

/** One place of the platform console and the permission that shows it (presentation only; the API refuses on its own). */
interface PlatformPlace extends DockLink {
  readonly permission: Permission;
}

/** The platform console's places: the platform's own things first, then the platform tenant's own people. */
const platformPlaces = (): ShellPlaces => {
  const access = inject(AccessStore);
  const platform = $localize`:@@ui.shell.nav.group.platform:Platform`;
  const administration = $localize`:@@ui.shell.nav.group.administration:Administration`;
  const home: DockLink = {
    label: $localize`:@@ui.shell.nav.home:Home`,
    link: ['/'],
    icon: 'home',
    exact: true,
  };
  const platformOnes: readonly PlatformPlace[] = [
    {
      label: $localize`:@@ui.shell.nav.tenants:Tenants`,
      link: ['/tenants'],
      icon: 'folio',
      permission: Permission.ViewTenants,
    },
    {
      label: $localize`:@@ui.shell.nav.properties:Properties`,
      link: ['/properties'],
      icon: 'rooms',
      permission: Permission.ViewTenants,
    },
    {
      label: $localize`:@@ui.shell.nav.reference:Reference data`,
      link: ['/reference'],
      icon: 'star',
      permission: Permission.ViewTenants,
    },
  ];
  const ownOnes: readonly PlatformPlace[] = [
    {
      label: $localize`:@@ui.shell.nav.people:People`,
      link: ['/people'],
      icon: 'guest',
      exact: true,
      permission: Permission.ViewUsers,
    },
    {
      label: $localize`:@@ui.shell.nav.roles:Roles`,
      link: ['/roles'],
      icon: 'tape',
      permission: Permission.ViewRoles,
    },
  ];
  const asDock = (place: PlatformPlace): DockLink =>
    place.exact === true
      ? { label: place.label, link: place.link, icon: place.icon, exact: true }
      : { label: place.label, link: place.link, icon: place.icon };
  const allowed = (places: readonly PlatformPlace[]): DockLink[] =>
    places.filter((place) => access.can(place.permission)).map(asDock);
  const dock = computed<readonly DockLink[]>(() => [...allowed(platformOnes), ...allowed(ownOnes)]);
  const items = computed<readonly NavItem[]>(() => [
    { ...home, group: platform },
    ...allowed(platformOnes).map((place) => ({ ...place, group: platform })),
    ...allowed(ownOnes).map((place) => ({ ...place, group: administration })),
  ]);
  return { scope: 'platform', items, dock };
};

export const providePlatformPlaces = (): Provider => ({
  provide: SHELL_PLACES,
  useFactory: platformPlaces,
});
