import { APP_BASE_HREF } from '@angular/common';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideHarmonyErrors } from '@harmony/platform/errors';
import { provideHarmonyHttp } from '@harmony/platform/http';
import { provideHarmonyTelemetry } from '@harmony/platform/telemetry';
import { provideHarmonyTitles } from '@harmony/ui/shell';
import { routes } from './app.routes';
import { provideHotelPlaces } from './places';

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
    provideHotelPlaces(),
    // No <base> element (the BFF page policy forbids it): the router learns the base here.
    { provide: APP_BASE_HREF, useValue: '/' },
  ],
};
