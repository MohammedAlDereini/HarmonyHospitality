/**
 * The web app's only door to Identity. Two kinds of calls:
 *  - Duende's token endpoint (form posts): login, second factor, refresh, revocation
 *  - the API envelope (json): { data, errors: [{ code }] }; 200/201 ok, 202 business refusal, 400 validation, 403 no permission
 * Signed-in calls go through authCall, which refreshes an expired access token once and ends the session when that fails.
 */
import { clearSession, getTokens, setTokens } from './session'

const base = (import.meta.env.VITE_IDENTITY_URL as string | undefined)?.replace(/\/$/, '') ?? 'https://localhost:5000'
const clientId = 'harmony-web'

export type Tokens = { access_token: string; refresh_token: string; expires_in: number }

export type LoginResult =
  | { kind: 'ok'; tokens: Tokens }
  | { kind: 'mfa'; mfaToken: string }
  | { kind: 'error'; error: string }

export type ApiResult<T = unknown> = { ok: boolean; status: number; codes: string[]; data?: T }

type Method = 'GET' | 'POST' | 'PUT'

/** Identity not reachable, or the browser refused the call (origin not allowed): one code for both. */
export const unreachable = 'identity_unreachable'
/** The access token expired and could not be refreshed: the person has to sign in again. */
export const sessionExpired = 'session_expired'

async function tokenCall(form: Record<string, string>): Promise<LoginResult> {
  let response: Response
  try {
    response = await fetch(`${base}/connect/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ client_id: clientId, scope: 'harmony offline_access', ...form }),
    })
  } catch {
    return { kind: 'error', error: unreachable }
  }
  const body = (await response.json().catch(() => ({}))) as Record<string, unknown>
  if (response.ok && typeof body.access_token === 'string') {
    return { kind: 'ok', tokens: body as Tokens }
  }
  if (body.error_description === 'mfa_required' && typeof body.mfa_token === 'string') {
    return { kind: 'mfa', mfaToken: body.mfa_token }
  }
  return { kind: 'error', error: String(body.error_description ?? body.error ?? `http_${response.status}`) }
}

export const login = (username: string, password: string) =>
  tokenCall({ grant_type: 'password', username, password })

export const completeMfa = (mfaToken: string, proof: { otp?: string; recoveryCode?: string }) =>
  tokenCall({
    grant_type: 'mfa_otp',
    mfa_token: mfaToken,
    ...(proof.otp ? { otp: proof.otp } : {}),
    ...(proof.recoveryCode ? { recovery_code: proof.recoveryCode } : {}),
  })

export const refresh = (refreshToken: string) =>
  tokenCall({ grant_type: 'refresh_token', refresh_token: refreshToken })

/** Logout of this device: the refresh token dies on the server. */
export async function revoke(refreshToken: string): Promise<void> {
  await fetch(`${base}/connect/revocation`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ client_id: clientId, token: refreshToken, token_type_hint: 'refresh_token' }),
  }).catch(() => undefined)
}

async function apiCall<T>(method: Method, path: string, body?: unknown, accessToken?: string): Promise<ApiResult<T>> {
  let response: Response
  try {
    response = await fetch(`${base}${path}`, {
      method,
      headers: {
        ...(method === 'GET' ? {} : { 'Content-Type': 'application/json' }),
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      ...(method === 'GET' ? {} : { body: JSON.stringify(body ?? {}) }),
    })
  } catch {
    return { ok: false, status: 0, codes: [unreachable] }
  }
  const envelope = (await response.json().catch(() => ({}))) as { data?: T; errors?: { code: string }[] }
  const codes = (envelope.errors ?? []).map((e) => e.code)
  if (response.status === 403 && codes.length === 0) codes.push('forbidden')
  return { ok: response.status < 300 && codes.length === 0, status: response.status, codes, data: envelope.data }
}

/**
 * A call on behalf of the signed-in person. A 401 means the 15-minute access token has expired: refresh once and retry;
 * if the refresh is refused too (revoked, logged out everywhere, password changed) the session is over.
 */
export async function authCall<T>(method: Method, path: string, body?: unknown): Promise<ApiResult<T>> {
  const tokens = getTokens()
  if (!tokens) return { ok: false, status: 401, codes: [sessionExpired] }
  let result = await apiCall<T>(method, path, body, tokens.access_token)
  if (result.status !== 401) return result
  const renewed = await refresh(tokens.refresh_token)
  if (renewed.kind !== 'ok') {
    clearSession()
    return { ok: false, status: 401, codes: [sessionExpired] }
  }
  setTokens(renewed.tokens)
  result = await apiCall<T>(method, path, body, renewed.tokens.access_token)
  if (result.status === 401) {
    clearSession()
    return { ok: false, status: 401, codes: [sessionExpired] }
  }
  return result
}

// ---- account (anonymous flows keep an explicit token argument: they run before or outside a session) ----

export const forgotPassword = (email: string) =>
  apiCall('POST', '/api/UserAccount/ForgotPassword', { email })

export const resetPassword = (email: string, token: string, newPassword: string) =>
  apiCall('POST', '/api/UserAccount/ResetPassword', { email, token, newPassword })

export const changePassword = (accessToken: string, currentPassword: string, newPassword: string) =>
  apiCall('PUT', '/api/UserAccount/ChangePassword', { currentPassword, newPassword }, accessToken)

export const logoutEverywhere = (accessToken: string) =>
  apiCall('PUT', '/api/UserAccount/LogoutEverywhere', undefined, accessToken)

export type AccountModel = {
  id: string
  userName: string
  email: string | null
  displayName: string
  twoFactorEnabled: boolean
  state: string | number
  stateReason: string | null
  securityVersion: number
  createdDate: string
  modifiedDate: string | null
}

/** The signed-in person's own account row, when their role may read accounts (ViewUsers). */
export const myAccount = (id: string) => authCall<AccountModel>('GET', `/api/UserAccount/${id}`)

// ---- two-factor (always about the signed-in person) ----

export type TwoFactorSetup = { sharedKey: string; authenticatorUri: string }

export const setupTwoFactor = () => authCall<TwoFactorSetup>('POST', '/api/UserAccount/SetupTwoFactor')

export const enableTwoFactor = (code: string) =>
  authCall<{ recoveryCodes: string[] }>('PUT', '/api/UserAccount/EnableTwoFactor', { code })

export const disableTwoFactor = (currentPassword: string) =>
  authCall('PUT', '/api/UserAccount/DisableTwoFactor', { currentPassword })

// ---- reference data: the same four doors on every table ----

export type Paged<T> = { results: T[]; totalCount: number; pageNumber: number; pageSize: number }
export type SortOrder = { memberName: string; sortOrder: 1 | 2 }

export const listRows = <T>(api: string, body: Record<string, unknown>) =>
  authCall<Paged<T>>('POST', `/api/${api}/get-all`, body)

/** A time zone id keeps its slash (Asia/Amman: the server route is a catch-all); every other key is encoded as one segment. */
export const getRow = <T>(api: string, key: string) =>
  authCall<T>('GET', `/api/${api}/${key.split('/').map(encodeURIComponent).join('/')}`)

export const createRow = (api: string, body: Record<string, unknown>) =>
  authCall<string>('POST', `/api/${api}/Create`, body)

export const updateRow = (api: string, body: Record<string, unknown>) =>
  authCall('PUT', `/api/${api}/Update`, body)

/** The claims inside an access token (no signature check here: the server checks, the app only reads). */
export function claimsOf(accessToken: string): Record<string, unknown> {
  try {
    const payload = accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
    return JSON.parse(atob(payload.padEnd(payload.length + ((4 - (payload.length % 4)) % 4), '=')))
  } catch {
    return {}
  }
}
