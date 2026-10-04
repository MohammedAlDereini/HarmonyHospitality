import type { Tokens } from './api'

/**
 * Where the tokens live in the browser: sessionStorage, so they end with the tab and never reach another site.
 * A public web app without a backend-for-frontend has no safer place for a refresh token; the server side keeps
 * the real guards (15-minute access tokens, one-time refresh, SecurityVersion, logout everywhere).
 */
const key = 'harmony.session'

export function getTokens(): Tokens | null {
  try {
    const raw = sessionStorage.getItem(key)
    return raw ? (JSON.parse(raw) as Tokens) : null
  } catch {
    return null
  }
}

export function setTokens(tokens: Tokens): void {
  sessionStorage.setItem(key, JSON.stringify(tokens))
}

export function clearSession(): void {
  sessionStorage.removeItem(key)
}

export function hasSession(): boolean {
  return getTokens() !== null
}
