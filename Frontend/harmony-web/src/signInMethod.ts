/** How the person signed in, from the session's amr: a passkey ("swk"), the password with the authenticator ("mfa"), or the password. */
export function signInMethod(amr: string[] | undefined): string {
  const methods = amr ?? []
  if (methods.includes('swk')) return 'passkey'
  if (methods.includes('mfa')) return 'password + authenticator'
  return 'password'
}
