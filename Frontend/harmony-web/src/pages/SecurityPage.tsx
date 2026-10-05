import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import QRCode from 'qrcode'
import { claimsOf, disableTwoFactor, enableTwoFactor, logoutEverywhere, myAccount, sessionExpired, setupTwoFactor, type AccountModel, type TwoFactorSetup } from '../api'
import { messagesFor } from '../errors'
import { clearSession, getTokens } from '../session'
import { toast } from '../components/Toast'

/**
 * The signed-in person's security: two-step sign-in (set up with an authenticator app, recovery codes shown once,
 * turn off with the password) and the sessions (sign out everywhere). The account row is read when the role may read accounts.
 */
export default function SecurityPage() {
  const navigate = useNavigate()
  const tokens = getTokens()
  const claims = tokens ? claimsOf(tokens.access_token) : {}
  const sub = String(claims.sub ?? '')
  const [account, setAccount] = useState<AccountModel | null>(null)
  const [canRead, setCanRead] = useState(true)
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null)
  const [code, setCode] = useState('')
  const [recovery, setRecovery] = useState<string[] | null>(null)
  const [disabling, setDisabling] = useState(false)
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const canvasRef = useRef<HTMLCanvasElement>(null)

  async function loadAccount() {
    if (!sub) return
    const r = await myAccount(sub)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (r.ok && r.data) { setAccount(r.data); setCanRead(true) }
    else setCanRead(false)
  }

  useEffect(() => { void loadAccount() }, []) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (setup && canvasRef.current) {
      void QRCode.toCanvas(canvasRef.current, setup.authenticatorUri, { width: 180, margin: 1 })
    }
  }, [setup])

  async function onSetup() {
    setBusy(true); setError(null)
    const r = await setupTwoFactor()
    setBusy(false)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (!r.ok || !r.data) { setError(messagesFor(r.codes)); return }
    setSetup(r.data)
    setCode('')
  }

  async function onEnable(event: FormEvent) {
    event.preventDefault()
    setBusy(true); setError(null)
    const r = await enableTwoFactor(code.trim())
    setBusy(false)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (!r.ok || !r.data) { setError(messagesFor(r.codes)); return }
    setRecovery(r.data.recoveryCodes)
    setSetup(null)
    toast('Two-step sign-in is on.')
    void loadAccount()
  }

  async function onDisable(event: FormEvent) {
    event.preventDefault()
    setBusy(true); setError(null)
    const r = await disableTwoFactor(password)
    setBusy(false)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (!r.ok) { setError(messagesFor(r.codes)); return }
    setDisabling(false)
    setPassword('')
    setRecovery(null)
    toast('Two-step sign-in is off.')
    void loadAccount()
  }

  async function onSignOutEverywhere() {
    if (tokens) await logoutEverywhere(tokens.access_token)
    clearSession()
    navigate('/login', { replace: true })
  }

  const enabled = account?.twoFactorEnabled ?? null

  return (
    <>
      <header className="page">
        <div>
          <h1>Security</h1>
          <p className="lead">Two-step sign-in, your sessions and your password.</p>
        </div>
      </header>

      <div className="stack">
        <section className="surface pad">
          <h2>Two-step sign-in</h2>
          <p className="lead">
            {enabled === true && 'On. Signing in asks for a 6-digit code from your authenticator app after the password.'}
            {enabled === false && 'Off. Turn it on so a stolen password alone cannot sign in as you.'}
            {enabled === null && (canRead ? 'Checking…' : 'Your role cannot read account details, so the current state is not shown here; the actions below still work.')}
          </p>

          {recovery && (
            <div>
              <p className="ok">Save these recovery codes now. Each works once, for the day the phone is lost; they are not shown again.</p>
              <div className="codes">{recovery.map((c) => <code key={c}>{c}</code>)}</div>
              <button type="button" className="secondary auto" style={{ marginTop: 12 }} onClick={() => { void navigator.clipboard?.writeText(recovery.join('\n')); toast('Recovery codes copied.') }}>Copy the codes</button>
            </div>
          )}

          {!setup && !disabling && (
            <div style={{ display: 'flex', gap: 10, marginTop: 14, flexWrap: 'wrap' }}>
              {enabled !== true && <button type="button" className="auto" disabled={busy} onClick={onSetup}>Set up an authenticator app</button>}
              {enabled !== false && <button type="button" className="auto secondary" disabled={busy} onClick={() => { setDisabling(true); setError(null) }}>Turn off two-step sign-in</button>}
            </div>
          )}

          {setup && (
            <form onSubmit={onEnable}>
              <div className="qr">
                <canvas ref={canvasRef} width={180} height={180} aria-label="QR code for the authenticator app" />
                <div style={{ flex: 1, minWidth: 220 }}>
                  <p className="lead" style={{ marginBottom: 6 }}>Scan the code with Microsoft Authenticator, Google Authenticator or any TOTP app, or type the key:</p>
                  <p className="mono" style={{ wordBreak: 'break-all', margin: 0 }}>{setup.sharedKey}</p>
                  <label htmlFor="otp">Then enter the 6-digit code the app shows</label>
                  <input id="otp" inputMode="numeric" autoComplete="one-time-code" autoFocus value={code} onChange={(e) => setCode(e.target.value)} />
                  {error && <p className="error" role="alert">{error}</p>}
                  <div style={{ display: 'flex', gap: 10 }}>
                    <button type="submit" disabled={busy || code.trim().length !== 6}>Turn on</button>
                    <button type="button" className="secondary" onClick={() => { setSetup(null); setError(null) }}>Cancel</button>
                  </div>
                </div>
              </div>
            </form>
          )}

          {disabling && (
            <form onSubmit={onDisable} style={{ maxWidth: 420 }}>
              <label htmlFor="pw">Your current password, to confirm</label>
              <input id="pw" type="password" autoComplete="current-password" autoFocus value={password} onChange={(e) => setPassword(e.target.value)} />
              {error && <p className="error" role="alert">{error}</p>}
              <div style={{ display: 'flex', gap: 10 }}>
                <button type="submit" className="danger" disabled={busy || !password}>Turn off</button>
                <button type="button" className="secondary" onClick={() => { setDisabling(false); setError(null); setPassword('') }}>Cancel</button>
              </div>
            </form>
          )}

          {!setup && !disabling && error && <p className="error" role="alert">{error}</p>}
        </section>

        <section className="surface pad">
          <h2>Sessions</h2>
          <p className="lead">Signing out everywhere ends every session on every device, this one included, and invalidates every refresh token.</p>
          <ul className="facts">
            <li><span>Signed in as</span><span>{String(claims.name ?? '')}</span></li>
            <li><span>Signed in with</span><span>{String(claims.amr ?? '').includes('mfa') ? 'password + authenticator' : 'password'}</span></li>
            <li><span>Security version</span><span>{String(account?.securityVersion ?? claims.security_version ?? '')}</span></li>
          </ul>
          <div style={{ display: 'flex', gap: 10, marginTop: 14, flexWrap: 'wrap' }}>
            <button type="button" className="auto secondary" onClick={() => navigate('/account/change-password')}>Change password</button>
            <button type="button" className="auto danger" onClick={onSignOutEverywhere}>Sign out everywhere</button>
          </div>
        </section>
      </div>
    </>
  )
}
