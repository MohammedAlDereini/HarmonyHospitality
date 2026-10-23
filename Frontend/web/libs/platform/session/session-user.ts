/** What GET /bff/user answers: the few claims the app needs to draw itself, never a token (architecture 11.1). */
export interface SessionUser {
  readonly sub: string;
  readonly name: string | null;
  readonly tenant: string | null;
  readonly roles: readonly string[];
  /** How the person signed in: pwd, and mfa when the authenticator was used. */
  readonly amr: readonly string[];
  /** The sign-out door with this session's own sid; a navigation, never a fetch. */
  readonly logoutUrl: string;
}
