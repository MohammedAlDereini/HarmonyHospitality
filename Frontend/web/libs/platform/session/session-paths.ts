/** The BFF's doors the session walks through (architecture 11.1, 11.2). */
export const userPath = '/bff/user';
export const signInPath = '/bff/login';
/** The small page a separate sign-in window lands on; it tells the original tab and closes. */
export const signedInRoute = 'signed-in';
export const signedInPath = `/${signedInRoute}`;
/** Where a person lands after signing out; outside the signed-in frame, so nothing bounces them to sign-in. */
export const signedOutRoute = 'signed-out';

/** A control character: never part of a path this app writes. */
const isControl = (character: string): boolean => character < ' ' || character === '\u007f';

/**
 * Mirrors the BFF's own rule: "/reservations" is welcome; "//evil.example", "/\evil.example" (browsers read the
 * backslash as a slash) and "https://evil.example" fall back to home. Only a path of this app ever rides along as a
 * return path (rule 15, sign-in return paths).
 */
export const localPathOnly = (path: string): string =>
  path.startsWith('/') &&
  !path.startsWith('//') &&
  !path.includes('\\') &&
  !path.includes(':') &&
  !Array.from(path).some(isControl)
    ? path
    : '/';

/** The sign-in door with its return path, built with URLSearchParams, never string-joined (rule 15). */
export const signInUrl = (returnUrl: string): string =>
  `${signInPath}?${new URLSearchParams({ returnUrl }).toString()}`;
