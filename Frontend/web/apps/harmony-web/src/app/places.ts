import { Provider, computed, inject } from '@angular/core';
import { AccessStore, Permission } from '@harmony/platform/access';
import { CurrentProperty } from '@harmony/platform/context';
import { DockLink, NavItem, SHELL_PLACES, ShellPlaces } from '@harmony/ui/shell';

/** The hotel app's places: Home always; the administration inside a property, with its permissions (presentation only). */
const hotelPlaces = (): ShellPlaces => {
  const access = inject(AccessStore);
  const current = inject(CurrentProperty);
  const home: DockLink = {
    label: $localize`:@@ui.shell.nav.home:Home`,
    link: ['/'],
    icon: 'home',
    exact: true,
  };
  const dock = computed<readonly DockLink[]>(() => {
    const code = current.code();
    if (code === null) return [];
    const users: DockLink[] = access.can(Permission.ViewUsers)
      ? [
          {
            label: $localize`:@@ui.shell.nav.users:Users`,
            link: ['/p', code, 'admin'],
            icon: 'guest',
            exact: true,
          },
        ]
      : [];
    const roles: DockLink[] = access.can(Permission.ViewRoles)
      ? [
          {
            label: $localize`:@@ui.shell.nav.roles:Roles`,
            link: ['/p', code, 'admin', 'roles'],
            icon: 'tape',
          },
        ]
      : [];
    return [...users, ...roles];
  });
  const items = computed<readonly NavItem[]>(() => [
    { ...home, group: $localize`:@@ui.shell.nav.group.workspaces:Workspaces` },
    ...dock().map((place) => ({
      ...place,
      group: $localize`:@@ui.shell.nav.group.administration:Administration`,
    })),
  ]);
  return { scope: 'property', items, dock };
};

export const provideHotelPlaces = (): Provider => ({
  provide: SHELL_PLACES,
  useFactory: hotelPlaces,
});
