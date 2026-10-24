import { InjectionToken, Signal, signal } from '@angular/core';
import { IconName } from '@harmony/ui/icons';
import { NavItem } from './nav-panel';

/** One icon in the dock: the same place as a nav item, without the words. */
export interface DockLink {
  readonly label: string;
  readonly link: readonly string[];
  readonly icon: IconName;
  readonly exact?: true;
}

/**
 * What the shell shows for this app (architecture 8.3): the hotel app offers its workspaces inside a property, the
 * platform console offers the platform's own places and never a property scope.
 */
export interface ShellPlaces {
  /** 'property': the bar carries the operating scope and the hotel chooser; 'platform': it does not. */
  readonly scope: 'property' | 'platform';
  readonly items: Signal<readonly NavItem[]>;
  readonly dock: Signal<readonly DockLink[]>;
}

const nowhere: ShellPlaces = { scope: 'platform', items: signal([]), dock: signal([]) };

/** Each app provides its own; without one the shell shows no places at all. */
export const SHELL_PLACES = new InjectionToken<ShellPlaces>('SHELL_PLACES', {
  providedIn: 'root',
  factory: () => nowhere,
});
