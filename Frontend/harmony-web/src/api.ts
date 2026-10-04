/**
 * The web app's only door to Identity. Two kinds of calls:
 *  - Duende's token endpoint (form posts): login, second factor, refresh, revocation
 *  - the API envelope (json): { data, errors: [{ code }] }; 200/201 ok, 202 business refusal, 400 validation
 * Nothing here stores anything; see session.ts.
 */
const base = (import.meta.env.VITE_IDENTITY_URL as string | undefined)?.replace(/\/$/, '') ?? 'https://localhost:5000'
const clientId = 'harmony-web'

export type Tokens = { access_token: string; refresh_token: string; expires_in: number }

export type LoginResult =
  | { kind: 'ok'; tokens: Tokens }
  | { kind: 'mfa'; mfaToken: string }
  | { kind: 'error'; error: string }

export type ApiResult<T = unknown> = { ok: boolean; status: number; codes: string[]; data?: T }

/** Identity not reachable, or the browser refused the call (origin not allowed): one code for both. */
export const unreachable = 'identity_unreachable'

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

async function apiCall<T>(method: 'POST' | 'PUT', path: string, body?: unknown, accessToken?: string): Promise<ApiResult<T>> {
  let response: Response
  try {
    response = await fetch(`${base}${path}`, {
      method,
      headers: {
        'Content-Type': 'application/json',
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      body: JSON.stringify(body ?? {}),
    })
  } catch {
    return { ok: false, status: 0, codes: [unreachable] }
  }
  const envelope = (await response.json().catch(() => ({}))) as { data?: T; errors?: { code: string }[] }
  const codes = (envelope.errors ?? []).map((e) => e.code)
  return { ok: response.status < 300 && codes.length === 0, status: response.status, codes, data: envelope.data }
}

export const forgotPassword = (email: string) =>
  apiCall('POST', '/api/UserAccount/ForgotPassword', { email })

export const resetPassword = (email: string, token: string, newPassword: string) =>
  apiCall('POST', '/api/UserAccount/ResetPassword', { email, token, newPassword })

export const changePassword = (accessToken: string, currentPassword: string, newPassword: string) =>
  apiCall('PUT', '/api/UserAccount/ChangePassword', { currentPassword, newPassword }, accessToken)

export const logoutEverywhere = (accessToken: string) =>
  apiCall('PUT', '/api/UserAccount/LogoutEverywhere', undefined, accessToken)

/** The claims inside an access token (no signature check here: the server checks, the app only reads). */
export function claimsOf(accessToken: string): Record<string, unknown> {
  try {
    const payload = accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
    return JSON.parse(atob(payload.padEnd(payload.length + ((4 - (payload.length % 4)) % 4), '=')))
  } catch {
    return {}
  }
}
