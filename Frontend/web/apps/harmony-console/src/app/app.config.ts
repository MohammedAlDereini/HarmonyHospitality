import { APP_BASE_HREF } from '@angular/common';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { providePlatformInstants } from '@harmony/platform/context';
import { provideHarmonyErrors } from '@harmony/platform/errors';
import { provideHarmonyHttp } from '@harmony/platform/http';
import { provideHarmonyTelemetry } from '@harmony/platform/telemetry';
import { provideHarmonyTitles } from '@harmony/ui/shell';
import { routes } from './app.routes';
import { providePlatformPlaces } from './places';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled' }),
    ),
    provideHarmonyHttp(),
    provideHarmonyErrors(),
    // Failures and Core Web Vitals flow to /bff/telemetry as named events (D9).
    provideHarmonyTelemetry(),
    provideHarmonyTitles(),
    providePlatformPlaces(),
    // The platform has no property: instants are shown in UTC.
    providePlatformInstants(),
    // No <base> element (the BFF page policy forbids it): the router learns the base here.
    { provide: APP_BASE_HREF, useValue: '/' },
  ],
};
