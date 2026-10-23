import { HttpInterceptorFn } from '@angular/common/http';

// An absolute URL: a scheme ("https:", "mailto:") or a protocol-relative start ("//").
// Matches "https://x.test/a" and "//x.test/a"; rejects "/api/x" and "api/x".
// Tester: https://regex101.com/?regex=%5E%5Ba-z%5D%5Ba-z0-9%2B.-%5D*%3A&flags=i&testString=https%3A%2F%2Fx.test%2Fa%0A%2Fapi%2Fx
const absoluteUrl = /^[a-z][a-z0-9+.-]*:/i;

/** True for an absolute URL: never where the app's calls may go (architecture 11.2). */
export const isAbsoluteUrl = (url: string): boolean =>
  absoluteUrl.test(url) || url.startsWith('//');

/** Every call is relative and carries x-csrf: 1, a header no cross-site page can add. An absolute URL is a bug. */
export const csrfInterceptor: HttpInterceptorFn = (req, next) => {
  if (isAbsoluteUrl(req.url)) {
    throw new Error(`Only relative URLs reach the BFF: ${req.url}`);
  }
  return next(req.clone({ setHeaders: { 'x-csrf': '1' } }));
};
