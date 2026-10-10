import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { whoAmI, type User } from './api'

/**
 * The session lives on the BFF, behind one HttpOnly cookie this code can never read; the app only ever knows
 * WHO is signed in, from /bff/user, asked once when the page loads. Nothing about the session is kept here:
 * no token, no storage, nothing a script on this page could leak.
 */
type Session = { user: User | null; loading: boolean }

const SessionContext = createContext<Session>({ user: null, loading: true })

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session>({ user: null, loading: true })

  useEffect(() => {
    let alive = true
    void whoAmI().then((user) => {
      if (alive) setSession({ user, loading: false })
    })
    return () => { alive = false }
  }, [])

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>
}

export const useSession = () => useContext(SessionContext)

/**
 * Signing in is a top-level navigation to the BFF, which runs the whole dance with Identity and lands back on
 * returnTo. Only a local path may ride along: anything else falls back to home.
 */
export function goSignIn(returnTo = '/') {
  const local = returnTo.startsWith('/') && !returnTo.startsWith('//') ? returnTo : '/'
  window.location.replace(`/bff/login?returnUrl=${encodeURIComponent(local)}`)
}
