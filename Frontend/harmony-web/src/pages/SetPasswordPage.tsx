import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { resetPassword } from '../api'
import { messagesFor } from '../errors'

/**
 * The page both mails point at. The link carries email and token; the person only types the new password.
 * "reset" after a forgotten password, "invite" for a brand-new account: same call, different words.
 */
export default function SetPasswordPage({ mode }: { mode: 'reset' | 'invite' }) {
  const [params] = useSearchParams()
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [done, setDone] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const title = mode === 'invite' ? 'Welcome to Harmony' : 'Choose a new password'
  const lead = mode === 'invite'
    ? `Set the password for ${email}. Only you will know it.`
    : `A new password for ${email}.`

  if (!email || !token) {
    return (
      <div className="card">
        <h1>{title}</h1>
        <p className="error" role="alert">This link is incomplete. Open it exactly as it was sent to you.</p>
        <p className="links"><Link to="/forgot-password">Ask for a new link</Link></p>
      </div>
    )
  }

  if (done) {
    return (
      <div className="card">
        <h1>Password saved</h1>
        <p className="lead">Sign in with your new password. Any earlier session was ended.</p>
        <p className="links"><Link to="/login">Sign in</Link></p>
      </div>
    )
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (password !== confirm) {
      setError('The two passwords do not match.')
      return
    }
    setBusy(true)
    setError(null)
    const result = await resetPassword(email, token, password)
    setBusy(false)
    if (result.ok) setDone(true)
    else setError(messagesFor(result.codes))
  }

  return (
    <form className="card" onSubmit={onSubmit}>
      <h1>{title}</h1>
      <p className="lead">{lead}</p>
      <label htmlFor="password">New password</label>
      <input id="password" name="password" type="password" autoComplete="new-password" autoFocus value={password} onChange={(e) => setPassword(e.target.value)} />
      <label htmlFor="confirm">Repeat it</label>
      <input id="confirm" name="confirm" type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
      <p className="lead">At least 12 characters, with an upper-case letter, a lower-case letter and a digit.</p>
      {error && <p className="error" role="alert">{error}</p>}
      <button type="submit" disabled={busy || !password || !confirm}>Save the password</button>
    </form>
  )
}
