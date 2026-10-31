import { InjectionToken, Signal, signal } from '@angular/core';
import { IconName } from '@harmony/ui/icons';
import { NavItem } from '@harmony/ui/shell';

/** One place in the console's side navigation: words, an icon, and the route. */
export interface ConsoleLink {
  readonly label: string;
  readonly link: readonly string[];
  readonly icon: IconName;
  readonly exact?: true;
  /** A small number beside the words, when the place has something waiting. */
  readonly count?: number;
}

/** One heading of the side navigation (PLATFORM, OPERATIONS, ADMINISTRATION) and the places under it. */
export interface ConsoleSection {
  readonly heading: string;
  readonly items: readonly ConsoleLink[];
}

/**
 * What the console shell shows for the app (the Figma's Side nav 4:68): the sections, the places pinned at the
 * foot (Settings), the four the phone's bottom bar keeps, and every place flat for the Ctrl K palette.
 */
export interface ConsolePlaces {
  readonly sections: Signal<readonly ConsoleSection[]>;
  readonly bottom: Signal<readonly ConsoleLink[]>;
  readonly phone: Signal<readonly ConsoleLink[]>;
  readonly items: Signal<readonly NavItem[]>;
}

/** One thing that needs a platform person now (the bell): what, where, how pressing. */
export interface AttentionItem {
  readonly id: string;
  readonly kind: string;
  readonly text: string;
  readonly link: readonly string[];
  readonly tone: 'stop' | 'warn' | 'info' | 'ok';
}

const nowhere: ConsolePlaces = {
  sections: signal([]),
  bottom: signal([]),
  phone: signal([]),
  items: signal([]),
};

/** The app provides its places; without them the shell shows an empty rail. */
export const CONSOLE_PLACES = new InjectionToken<ConsolePlaces>('CONSOLE_PLACES', {
  providedIn: 'root',
  factory: () => nowhere,
});

/** The bell's list; the app provides it from its own signals, the default rings for nothing. */
export const CONSOLE_ATTENTION = new InjectionToken<Signal<readonly AttentionItem[]>>(
  'CONSOLE_ATTENTION',
  { providedIn: 'root', factory: () => signal([]) },
);
