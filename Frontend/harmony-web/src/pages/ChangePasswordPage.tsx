import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { changePassword, claimsOf } from '../api'
import { messagesFor } from '../errors'
import { clearSession, getTokens } from '../session'

/**
 * The signed-in person changes the password. A forced change (first login) lands here too. On success every
 * session is ended by the server, this one included, so the page clears the local session and asks to sign in again.
 */
export default function ChangePasswordPage() {
  const navigate = useNavigate()
  const tokens = getTokens()
  const forced = Boolean(tokens && claimsOf(tokens.access_token).must_change_password)
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (!tokens) return
    if (next !== confirm) {
      setError('The two passwords do not match.')
      return
    }
    setBusy(true)
    setError(null)
    const result = await changePassword(tokens.access_token, current, next)
    setBusy(false)
    if (result.status === 401) {
      clearSession()
      navigate('/login', { replace: true })
      return
    }
    if (!result.ok) {
      setError(messagesFor(result.codes))
      return
    }
    clearSession()
    navigate('/login?changed=1', { replace: true })
  }

  return (
    <form className="card" onSubmit={onSubmit}>
      <h1>{forced ? 'Choose your own password' : 'Change your password'}</h1>
      <p className="lead">{forced ? 'The password you signed in with was set by someone else. Replace it before continuing.' : 'You will sign in again afterwards, on every device.'}</p>
      <label htmlFor="current">Current password</label>
      <input id="current" name="current" type="password" autoComplete="current-password" autoFocus value={current} onChange={(e) => setCurrent(e.target.value)} />
      <label htmlFor="next">New password</label>
      <input id="next" name="next" type="password" autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} />
      <label htmlFor="confirm">Repeat it</label>
      <input id="confirm" name="confirm" type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
      <p className="lead">At least 12 characters, with an upper-case letter, a lower-case letter and a digit.</p>
      {error && <p className="error" role="alert">{error}</p>}
      <button type="submit" disabled={busy || !current || !next || !confirm}>Save the password</button>
      {!forced && <button type="button" className="secondary" onClick={() => navigate('/')}>Cancel</button>}
    </form>
  )
}
