import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import { claimsOf, logoutEverywhere, revoke } from '../api'
import { clearSession, getTokens } from '../session'
import { tables } from '../reference/tables'
import { Lockup } from './Logo'
import { ToastHost } from './Toast'

/** The signed-in frame: global bar (brand, who), the rail (Home, the reference tables, the account) and the work surface. */
export default function Shell() {
  const navigate = useNavigate()
  const tokens = getTokens()
  const claims = tokens ? claimsOf(tokens.access_token) : {}
  const name = String(claims.name ?? 'Signed in')
  const initials = name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]?.toUpperCase() ?? '').join('') || 'H'
  const [open, setOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    function onDown(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setOpen(false)
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  async function signOut() {
    if (tokens) await revoke(tokens.refresh_token)
    clearSession()
    navigate('/login', { replace: true })
  }

  async function signOutEverywhere() {
    if (tokens) await logoutEverywhere(tokens.access_token)
    clearSession()
    navigate('/login', { replace: true })
  }

  return (
    <div className="shell">
      <header className="bar">
        <Link to="/" className="brand" aria-label="Harmony, home">
          <Lockup height={17} />
          <small>Platform</small>
        </Link>
        <div className="spacer" />
        <div className="who" ref={menuRef}>
          <button type="button" aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen(!open)}>
            <span className="avatar" aria-hidden="true">{initials}</span>
            <span>{name}</span>
          </button>
          {open && (
            <div className="menu" role="menu">
              <div className="head">
                <b>{name}</b>
                <span>{String(claims.amr ?? '').includes('mfa') ? 'Signed in with password and authenticator' : 'Signed in with password'}</span>
              </div>
              <button type="button" role="menuitem" onClick={() => { setOpen(false); navigate('/account/security') }}>Security and two-step sign-in</button>
              <button type="button" role="menuitem" onClick={() => { setOpen(false); navigate('/account/change-password') }}>Change password</button>
              <button type="button" role="menuitem" onClick={signOut}>Sign out of this device</button>
              <button type="button" role="menuitem" className="danger" onClick={signOutEverywhere}>Sign out everywhere</button>
            </div>
          )}
        </div>
      </header>
      <div className="body">
        <nav className="rail" aria-label="Sections">
          <NavLink to="/" end>Home</NavLink>
          <div className="group">Reference data</div>
          {tables.map((t) => (
            <NavLink key={t.slug} to={`/reference/${t.slug}`}>{t.title}</NavLink>
          ))}
          <div className="group">Account</div>
          <NavLink to="/account/security">Security</NavLink>
        </nav>
        <main className="work">
          <Outlet />
        </main>
      </div>
      <ToastHost />
    </div>
  )
}
