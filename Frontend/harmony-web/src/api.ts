/**
 * The web app's only door: the BFF, on this same origin. The browser holds one HttpOnly cookie; the BFF turns it
 * into the Bearer token the platform API wants, so no token ever exists on this page. Every call carries
 * "x-csrf: 1" — a header a cross-site page cannot add — and the API envelope stays { data, errors: [{ code }] }:
 * 200/201 ok, 202 business refusal, 400 validation, 403 no permission, 401 no session (sign in again).
 */
export type ApiResult<T = unknown> = { ok: boolean; status: number; codes: string[]; data?: T }

type Method = 'GET' | 'POST' | 'PUT'

/** The BFF not reachable: one code for every network failure. */
export const unreachable = 'identity_unreachable'
/** No session behind the cookie any more (it ended, or was ended for the person): sign in again. */
export const sessionExpired = 'session_expired'

const csrf = { 'x-csrf': '1' }

async function apiCall<T>(method: Method, path: string, body?: unknown, receipt?: string): Promise<ApiResult<T>> {
  let response: Response
  try {
    response = await fetch(path, {
      method,
      headers: {
        ...csrf,
        ...(method === 'GET' ? {} : { 'Content-Type': 'application/json' }),
        ...(receipt ? { 'Idempotency-Key': receipt } : {}),
      },
      ...(method === 'GET' ? {} : { body: JSON.stringify(body ?? {}) }),
    })
  } catch {
    return { ok: false, status: 0, codes: [unreachable] }
  }
  const envelope = (await response.json().catch(() => ({}))) as { data?: T; errors?: { code: string }[] }
  const codes = (envelope.errors ?? []).map((e) => e.code)
  if (response.status === 401) codes.push(sessionExpired)
  if (response.status === 403 && codes.length === 0) codes.push('forbidden')
  return { ok: response.status < 300 && codes.length === 0, status: response.status, codes, data: envelope.data }
}

/**
 * A call on behalf of the signed-in person: the cookie goes along by itself and the BFF keeps the token fresh,
 * so there is nothing to refresh or retry here. One receipt per user action: the server runs it once.
 */
export const authCall = <T>(method: Method, path: string, body?: unknown) =>
  apiCall<T>(method, path, body, method === 'GET' ? undefined : crypto.randomUUID())

// ---- who is signed in ----

export type User = {
  sub: string
  name: string
  tenant: string | null
  roles: string[]
  /** How the person signed in: "pwd", and "mfa" when the authenticator was used too. */
  amr: string[]
  /** The sign-out link, carrying this session's own proof; always a top-level navigation. */
  logoutUrl: string
}

/** The signed-in person, or null when there is no session (the BFF answers 401). */
export async function whoAmI(): Promise<User | null> {
  try {
    const response = await fetch('/bff/user', { headers: csrf })
    return response.ok ? ((await response.json()) as User) : null
  } catch {
    return null
  }
}

// ---- account (the anonymous flows run before a session exists) ----

export const forgotPassword = (email: string) =>
  apiCall('POST', '/api/UserAccount/ForgotPassword', { email })

export const resetPassword = (email: string, token: string, newPassword: string) =>
  apiCall('POST', '/api/UserAccount/ResetPassword', { email, token, newPassword })

export const changePassword = (currentPassword: string, newPassword: string) =>
  authCall('PUT', '/api/UserAccount/ChangePassword', { currentPassword, newPassword })

export const logoutEverywhere = () =>
  authCall('PUT', '/api/UserAccount/LogoutEverywhere')

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
