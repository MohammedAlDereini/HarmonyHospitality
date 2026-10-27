import { HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { csrfInterceptor } from './csrf.interceptor';
import { errorStatusInterceptor } from './error-status.interceptor';
import { traceContextInterceptor } from './trace-context.interceptor';

/**
 * HttpClient with Harmony's three interceptors, in this order: the CSRF header, the trace id, the status events.
 * An app may add its own after the CSRF check and before the trace id (harmony-web names the property).
 */
export function provideHarmonyHttp(extra: readonly HttpInterceptorFn[] = []): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideHttpClient(
      withInterceptors([
        csrfInterceptor,
        ...extra,
        traceContextInterceptor,
        errorStatusInterceptor,
      ]),
    ),
  ]);
}
