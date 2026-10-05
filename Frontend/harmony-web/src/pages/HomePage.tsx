import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { claimsOf, listRows, sessionExpired } from '../api'
import { getTokens } from '../session'
import { tables, type Row } from '../reference/tables'

/** Home: the reference tables with how many rows each holds, and who is signed in. */
export default function HomePage() {
  const navigate = useNavigate()
  const tokens = getTokens()
  const claims = tokens ? claimsOf(tokens.access_token) : {}
  const [counts, setCounts] = useState<Record<string, number | 'denied' | null>>({})

  useEffect(() => {
    let alive = true
    void Promise.all(
      tables.map(async (t) => {
        const r = await listRows<Row>(t.api, { pageNumber: 1, pageSize: 1 })
        if (r.codes.includes(sessionExpired)) navigate('/login', { replace: true })
        return [t.slug, r.ok && r.data ? r.data.totalCount : r.status === 403 ? 'denied' : null] as const
      }),
    ).then((pairs) => {
      if (alive) setCounts(Object.fromEntries(pairs))
    })
    return () => { alive = false }
  }, [navigate])

  const empty = tables.filter((t) => counts[t.slug] === 0).length

  return (
    <>
      <header className="page">
        <div>
          <h1>Reference data</h1>
          <p className="lead">
            The lists every hotel in Harmony shares: countries, cities, currencies, time zones, languages, property types and the regimes they live under.
            Kept here by the platform team; a hotel only reads them.
          </p>
        </div>
      </header>

      {empty > 0 && Object.keys(counts).length === tables.length && (
        <p className="note" style={{ margin: '0 0 12px' }}>
          {empty === tables.length
            ? 'Every table is empty: this is a fresh installation. Start with currencies and time zones, then countries, then the rest; each table checks that the rows it points at already exist.'
            : `${empty} ${empty === 1 ? 'table is' : 'tables are'} still empty.`}
        </p>
      )}

      <div className="grid">
        {tables.map((t) => {
          const n = counts[t.slug]
          return (
            <Link key={t.slug} to={`/reference/${t.slug}`} className="surface tile">
              <div className={n === undefined || n === null || n === 'denied' ? 'n dim' : 'n'}>
                {n === undefined ? '…' : n === 'denied' ? 'No access' : n === null ? 'Unavailable' : n}
              </div>
              <div className="t">{t.title}</div>
              <div className="d">{t.description}</div>
            </Link>
          )
        })}
      </div>

      <section className="surface pad" style={{ marginTop: 18, maxWidth: 520 }}>
        <h2>You</h2>
        <ul className="facts">
          <li><span>Signed in as</span><span>{String(claims.name ?? '')}</span></li>
          <li><span>Signed in with</span><span>{String(claims.amr ?? '').includes('mfa') ? 'password + authenticator' : 'password'}</span></li>
          <li><span>Roles</span><span>{Array.isArray(claims.role) ? claims.role.length : claims.role ? 1 : 0}</span></li>
        </ul>
        <p className="links"><Link to="/account/security">Security and two-step sign-in</Link></p>
      </section>
    </>
  )
}
