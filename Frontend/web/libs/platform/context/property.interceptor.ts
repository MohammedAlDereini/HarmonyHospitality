import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CurrentProperty } from './current-property';
import { MyProperties } from './my-properties';

/** The request header Identity reads: which hotel the call is about (the framework's RequestHeaders.ClientProperty). */
export const propertyHeader = 'X-Harmony-Property';

/** Only the API's own doors are about a hotel; the BFF's (session, telemetry) are not. */
const apiPrefix = '/api/';

/**
 * Inside /p/:property every API call names the hotel by id, so Identity and every service answer for that hotel:
 * its roles join the tenant-wide ones, and a hotel the person may not act at is refused (000.10). Outside a
 * property, or before the person's hotels are known, the call goes as it is.
 */
export const propertyInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(apiPrefix)) return next(req);
  const code = inject(CurrentProperty).code();
  if (code === null) return next(req);
  const property = inject(MyProperties)
    .list()
    .find((candidate) => candidate.code === code);
  if (property === undefined) return next(req);
  return next(req.clone({ setHeaders: { [propertyHeader]: property.id } }));
};
