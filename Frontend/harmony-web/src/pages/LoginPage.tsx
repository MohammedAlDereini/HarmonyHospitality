import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { claimsOf, completeMfa, login } from '../api'
import { messageFor } from '../errors'
import { setTokens } from '../session'
import type { Tokens } from '../api'
import { Lockup } from '../components/Logo'

/**
 * Sign-in in one or two steps: password, then, only when the account has an authenticator, the 6-digit code
 * (or a recovery code). A token that says the password must be changed sends the person straight to that page.
 */
export default function LoginPage() {
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [mfaToken, setMfaToken] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [useRecovery, setUseRecovery] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  function finish(tokens: Tokens) {
    setTokens(tokens)
    const claims = claimsOf(tokens.access_token)
    navigate(claims.must_change_password ? '/account/change-password' : '/', { replace: true })
  }

  async function onPassword(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    const result = await login(email.trim(), password)
    setBusy(false)
    if (result.kind === 'ok') finish(result.tokens)
    else if (result.kind === 'mfa') setMfaToken(result.mfaToken)
    else setError(messageFor(result.error))
  }

  async function onCode(event: FormEvent) {
    event.preventDefault()
    if (!mfaToken) return
    setBusy(true)
    setError(null)
    const result = await completeMfa(mfaToken, useRecovery ? { recoveryCode: code.trim() } : { otp: code.trim() })
    setBusy(false)
    if (result.kind === 'ok') finish(result.tokens)
    else if (result.kind === 'error' && result.error === 'mfa_token_invalid') {
      setMfaToken(null)
      setCode('')
      setError(messageFor(result.error))
    } else if (result.kind === 'error') setError(messageFor(result.error))
  }

  if (mfaToken) {
    return (
      <form className="card" onSubmit={onCode}>
        <h1>Second step</h1>
        <p className="lead">{useRecovery ? 'Enter one of your recovery codes.' : 'Enter the 6-digit code from your authenticator app.'}</p>
        <label htmlFor="code">{useRecovery ? 'Recovery code' : 'Code'}</label>
        <input id="code" name="code" autoFocus autoComplete="one-time-code" inputMode={useRecovery ? 'text' : 'numeric'} value={code} onChange={(e) => setCode(e.target.value)} />
        {error && <p className="error" role="alert">{error}</p>}
        <button type="submit" disabled={busy || !code}>Continue</button>
        <button type="button" className="secondary" onClick={() => { setUseRecovery(!useRecovery); setCode(''); setError(null) }}>
          {useRecovery ? 'Use the authenticator app instead' : 'Use a recovery code instead'}
        </button>
      </form>
    )
  }

  return (
    <form className="card" onSubmit={onPassword}>
      <div className="brand"><Lockup height={20} /></div>
      <h1>Sign in</h1>
      <p className="lead">Your work e-mail and password.</p>
      <label htmlFor="email">E-mail</label>
      <input id="email" name="email" type="email" autoComplete="username" autoFocus value={email} onChange={(e) => setEmail(e.target.value)} />
      <label htmlFor="password">Password</label>
      <input id="password" name="password" type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      {error && <p className="error" role="alert">{error}</p>}
      <button type="submit" disabled={busy || !email || !password}>Sign in</button>
      <p className="links"><Link to="/forgot-password">Forgot your password?</Link></p>
    </form>
  )
}
