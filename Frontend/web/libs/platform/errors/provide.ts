import { ErrorHandler, makeEnvironmentProviders } from '@angular/core';
import { HarmonyErrorHandler } from './harmony-error-handler';

/** Replaces Angular's error handler with ours; provideBrowserGlobalErrorListeners() in the app feeds it window errors. */
export const provideHarmonyErrors = () =>
  makeEnvironmentProviders([{ provide: ErrorHandler, useClass: HarmonyErrorHandler }]);
