/**
 * unknown until the first read of /bff/user; signed-out when nobody is signed in; expired when a session the page
 * had is gone (a 401, or another tab signed out); unavailable when the BFF could not answer at all.
 */
export type SessionState = 'unknown' | 'signed-in' | 'signed-out' | 'expired' | 'unavailable';
