import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { csrfInterceptor } from './csrf.interceptor';
import { errorStatusInterceptor } from './error-status.interceptor';
import { traceContextInterceptor } from './trace-context.interceptor';

/** HttpClient with Harmony's three interceptors, in this order: the CSRF header, the trace id, the status events. */
export function provideHarmonyHttp(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideHttpClient(
      withInterceptors([csrfInterceptor, traceContextInterceptor, errorStatusInterceptor]),
    ),
  ]);
}
