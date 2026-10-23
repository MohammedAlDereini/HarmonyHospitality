import { APP_BASE_HREF } from '@angular/common';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHarmonyErrors } from '@harmony/platform/errors';
import { provideHarmonyHttp } from '@harmony/platform/http';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHarmonyHttp(),
    provideHarmonyErrors(),
    // No <base> element (the BFF page policy forbids it): the router learns the base here.
    { provide: APP_BASE_HREF, useValue: '/' },
  ],
};
