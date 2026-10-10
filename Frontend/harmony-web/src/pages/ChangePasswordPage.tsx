import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { changePassword } from '../api'
import { messagesFor } from '../errors'

/**
 * The signed-in person changes the password. On success every session is ended by the server, this one included
 * (the BFF hears it server to server), so the page sends the person to sign in again.
 */
export default function ChangePasswordPage() {
  const navigate = useNavigate()
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (next !== confirm) {
      setError('The two passwords do not match.')
      return
    }
    setBusy(true)
    setError(null)
    const result = await changePassword(current, next)
    setBusy(false)
    if (result.status === 401) {
      navigate('/login', { replace: true })
      return
    }
    if (!result.ok) {
      setError(messagesFor(result.codes))
      return
    }
    navigate('/login', { replace: true })
  }

  return (
    <form className="card" onSubmit={onSubmit}>
      <h1>Change your password</h1>
      <p className="lead">You will sign in again afterwards, on every device.</p>
      <label htmlFor="current">Current password</label>
      <input id="current" name="current" type="password" autoComplete="current-password" autoFocus value={current} onChange={(e) => setCurrent(e.target.value)} />
      <label htmlFor="next">New password</label>
      <input id="next" name="next" type="password" autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} />
      <label htmlFor="confirm">Repeat it</label>
      <input id="confirm" name="confirm" type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
      <p className="lead">At least 12 characters with one symbol, like - or ! (a space counts). A few words make a strong password.</p>
      {error && <p className="error" role="alert">{error}</p>}
      <button type="submit" disabled={busy || !current || !next || !confirm}>Save the password</button>
      <button type="button" className="secondary" onClick={() => navigate('/')}>Cancel</button>
    </form>
  )
}
