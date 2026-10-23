import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap } from 'rxjs';
import { HttpStatus } from './http-status';
import { HttpStatusEvents } from './http-status-events';

/** Counts the answers the session cares about (401, 403); everything else passes through untouched. */
export const errorStatusInterceptor: HttpInterceptorFn = (req, next) => {
  const events = inject(HttpStatusEvents);
  return next(req).pipe(
    tap({
      error: (error: unknown) => {
        if (!(error instanceof HttpErrorResponse)) return;
        if (error.status === HttpStatus.unauthorized) events.unauthenticated.update((n) => n + 1);
        if (error.status === HttpStatus.forbidden) events.forbidden.update((n) => n + 1);
      },
    }),
  );
};
