import { APP_BASE_HREF } from '@angular/common';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { providePlatformInstants } from '@harmony/platform/context';
import { provideHarmonyErrors } from '@harmony/platform/errors';
import { provideHarmonyHttp } from '@harmony/platform/http';
import { provideHarmonyTelemetry } from '@harmony/platform/telemetry';
import { PAGE_ACTIONS_IN_HEAD, provideHarmonyTitles } from '@harmony/ui/shell';
import { routes } from './app.routes';
import { provideConsoleAttention } from './attention-bridge';
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
    // The console has no context bar: a page's actions sit in its own head, beside the title (Figma 20 Screens).
    { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
    // The bell rings for what the home lists: one store, read once for both.
    provideConsoleAttention(),
    // The platform has no property: instants are shown in UTC.
    providePlatformInstants(),
    // No <base> element (the BFF page policy forbids it): the router learns the base here.
    { provide: APP_BASE_HREF, useValue: '/' },
  ],
};
