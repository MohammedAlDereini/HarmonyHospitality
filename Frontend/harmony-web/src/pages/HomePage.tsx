import { useNavigate } from 'react-router-dom'
import { claimsOf, logoutEverywhere, revoke } from '../api'
import { clearSession, getTokens } from '../session'

/** The signed-in page: who you are according to the token, and the three doors a person has over their own account. */
export default function HomePage() {
  const navigate = useNavigate()
  const tokens = getTokens()
  const claims = tokens ? claimsOf(tokens.access_token) : {}
  const roles = Array.isArray(claims.role) ? claims.role.length : claims.role ? 1 : 0
  const amr = Array.isArray(claims.amr) ? claims.amr.join(', ') : String(claims.amr ?? '')

  async function onLogout() {
    if (tokens) await revoke(tokens.refresh_token)
    clearSession()
    navigate('/login', { replace: true })
  }

  async function onLogoutEverywhere() {
    if (tokens) await logoutEverywhere(tokens.access_token)
    clearSession()
    navigate('/login', { replace: true })
  }

  return (
    <div className="card">
      <h1>Harmony</h1>
      <p className="lead">Signed in as {String(claims.name ?? '')}.</p>
      <ul className="facts">
        <li><span>Account</span><span>{String(claims.sub ?? '')}</span></li>
        <li><span>Roles</span><span>{roles}</span></li>
        <li><span>Signed in with</span><span>{amr === 'mfa' ? 'password + authenticator' : 'password'}</span></li>
        <li><span>Security version</span><span>{String(claims.security_version ?? '')}</span></li>
      </ul>
      <button type="button" onClick={() => navigate('/account/change-password')}>Change password</button>
      <button type="button" className="secondary" onClick={onLogout}>Sign out of this device</button>
      <button type="button" className="danger" onClick={onLogoutEverywhere}>Sign out everywhere</button>
    </div>
  )
}
