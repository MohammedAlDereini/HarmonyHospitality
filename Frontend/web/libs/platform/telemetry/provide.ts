import { Injectable, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { Router } from '@angular/router';
import { ERROR_REPORT_SINK, ErrorReport, ErrorReportSink } from '@harmony/platform/errors';
import { Metric, onCLS, onINP, onLCP } from 'web-vitals';
import { routePatternOf } from './route-pattern';
import { Telemetry } from './telemetry';

/** Failures go to the BFF as events: the kind and code, the route pattern, never the message. */
@Injectable()
export class TelemetryErrorSink implements ErrorReportSink {
  private readonly telemetry = inject(Telemetry);

  send(report: ErrorReport): void {
    this.telemetry.record({
      name: `web.error.${report.kind}`,
      at: report.at,
      code: report.code,
      ...(report.routePattern === undefined ? {} : { route: report.routePattern }),
    });
  }
}

/** The three Core Web Vitals, as the web-vitals library measures them, each when it is final. */
const watchVitals = (telemetry: Telemetry, router: Router): void => {
  const report = (metric: Metric): void => {
    telemetry.record({
      name: `web.vital.${metric.name.toLowerCase()}`,
      at: new Date().toISOString(),
      value: metric.value,
      route: routePatternOf(router),
    });
  };
  onLCP(report);
  onINP(report);
  onCLS(report);
};

/** Registered in each app: failures and vitals flow to /bff/telemetry (architecture D9). */
export const provideHarmonyTelemetry = () =>
  makeEnvironmentProviders([
    TelemetryErrorSink,
    { provide: ERROR_REPORT_SINK, useExisting: TelemetryErrorSink },
    provideAppInitializer(() => {
      const telemetry = inject(Telemetry);
      watchVitals(telemetry, inject(Router));
      telemetry.flushWhenHidden();
    }),
  ]);
