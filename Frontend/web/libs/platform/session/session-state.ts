/** unknown until the first read of /bff/user; unavailable when the BFF could not answer at all. */
export type SessionState = 'unknown' | 'signed-in' | 'signed-out' | 'unavailable';
