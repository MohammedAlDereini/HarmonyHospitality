import { useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import { goSignIn } from '../session'
import { Lockup } from '../components/Logo'

/**
 * Sign-in happens on Identity's own pages (password, then the second step), reached through the BFF: this page
 * only sends the person there and comes back to where they were going. The app never sees a password.
 */
export default function LoginPage() {
  const [params] = useSearchParams()
  const returnTo = params.get('returnUrl') ?? '/'

  useEffect(() => {
    goSignIn(returnTo)
  }, [returnTo])

  return (
    <div className="card">
      <div className="brand"><Lockup height={20} /></div>
      <h1>Signing you in</h1>
      <p className="lead">Taking you to the sign-in page.</p>
    </div>
  )
}
