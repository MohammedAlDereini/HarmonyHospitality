import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { forgotPassword } from '../api'
import { messagesFor } from '../errors'

/** Asks Identity for a reset link. The answer is the same whether or not the address exists: nothing to probe. */
export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    const result = await forgotPassword(email.trim())
    setBusy(false)
    if (result.ok) setSent(true)
    else setError(messagesFor(result.codes))
  }

  if (sent) {
    return (
      <div className="card">
        <h1>Check your mail</h1>
        <p className="lead">If an account exists for {email.trim()}, a link to choose a new password is on its way. It works for 24 hours.</p>
        <p className="links"><Link to="/login">Back to sign in</Link></p>
      </div>
    )
  }

  return (
    <form className="card" onSubmit={onSubmit}>
      <h1>Forgot your password?</h1>
      <p className="lead">Enter your e-mail and we send you a link to choose a new one.</p>
      <label htmlFor="email">E-mail</label>
      <input id="email" name="email" type="email" autoComplete="username" autoFocus value={email} onChange={(e) => setEmail(e.target.value)} />
      {error && <p className="error" role="alert">{error}</p>}
      <button type="submit" disabled={busy || !email}>Send the link</button>
      <p className="links"><Link to="/login">Back to sign in</Link></p>
    </form>
  )
}
