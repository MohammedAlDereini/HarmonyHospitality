import { makeEnvironmentProviders } from '@angular/core';
import { InstantFormatter, UtcInstants } from './instant-formatter';
import { PropertyContext } from './property-context';

/** On the /p/:property route: one context for everything beneath it (architecture 10); it also tells the time. */
export const providePropertyContext = () =>
  makeEnvironmentProviders([
    PropertyContext,
    { provide: InstantFormatter, useExisting: PropertyContext },
  ]);

/** The platform console has no property: instants are shown in UTC. */
export const providePlatformInstants = () =>
  makeEnvironmentProviders([{ provide: InstantFormatter, useClass: UtcInstants }]);
