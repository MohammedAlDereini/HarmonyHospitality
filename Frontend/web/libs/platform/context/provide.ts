import { makeEnvironmentProviders } from '@angular/core';
import { PropertyContext } from './property-context';

/** On the /p/:property route: one context for everything beneath it (architecture 10). */
export const providePropertyContext = () => makeEnvironmentProviders([PropertyContext]);
