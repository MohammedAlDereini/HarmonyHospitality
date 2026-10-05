import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { Navigate, useNavigate, useParams } from 'react-router-dom'
import { createRow, listRows, sessionExpired, updateRow, type SortOrder } from '../../api'
import { messagesFor } from '../../errors'
import { tableBySlug, tables, type Field, type Row, type TableDef } from '../../reference/tables'
import { toast } from '../../components/Toast'

const pageSize = 20

/** One page for all ten reference tables: the list with its filters, sorting and paging, and the drawer that adds or edits a row. */
export default function ReferenceTablePage() {
  const { slug } = useParams()
  const def = tableBySlug(slug)
  if (!def) return <Navigate to="/" replace />
  return <TablePage key={def.slug} def={def} />
}

function TablePage({ def }: { def: TableDef }) {
  const navigate = useNavigate()
  const [filters, setFilters] = useState<Record<string, string>>({})
  const [applied, setApplied] = useState<Record<string, string>>({})
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState<SortOrder[]>(def.defaultSort)
  const [rows, setRows] = useState<Row[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [drawer, setDrawer] = useState<{ mode: 'create' } | { mode: 'edit'; row: Row } | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    const body: Record<string, unknown> = { pageNumber: page, pageSize, sortOrder: sort }
    for (const [k, v] of Object.entries(applied)) {
      if (v === '') continue
      body[k] = v === 'true' ? true : v === 'false' ? false : v
    }
    const result = await listRows<Row>(def.api, body)
    setLoading(false)
    if (result.codes.includes(sessionExpired)) {
      navigate('/login', { replace: true })
      return
    }
    if (!result.ok || !result.data) {
      setError(messagesFor(result.codes))
      setRows([])
      setTotal(0)
      return
    }
    setRows(result.data.results)
    setTotal(result.data.totalCount)
  }, [def.api, page, sort, applied, navigate])

  useEffect(() => {
    void load()
  }, [load])

  function onFilter(event: FormEvent) {
    event.preventDefault()
    setPage(1)
    setApplied(filters)
  }

  function clearFilters() {
    setFilters({})
    setApplied({})
    setPage(1)
  }

  function toggleSort(member: string) {
    setPage(1)
    setSort((current) => {
      const first = current[0]
      if (first && first.memberName === member) return [{ memberName: member, sortOrder: first.sortOrder === 1 ? 2 : 1 }]
      return [{ memberName: member, sortOrder: 1 }]
    })
  }

  const pages = Math.max(1, Math.ceil(total / pageSize))
  const firstSort = sort[0]

  return (
    <>
      <header className="page">
        <div>
          <h1>{def.title}</h1>
          <p className="lead">{def.description}</p>
        </div>
        <button type="button" className="auto" onClick={() => setDrawer({ mode: 'create' })}>Add {def.singular}</button>
      </header>

      <section className="surface">
        <form className="toolbar" onSubmit={onFilter}>
          {def.filters.map((f) => (
            <div key={f.name} className={f.name === 'searchText' ? 'field wide' : 'field'}>
              <label htmlFor={`f-${f.name}`}>{f.label}</label>
              <FilterInput field={f} id={`f-${f.name}`} value={filters[f.name] ?? ''} values={filters} onChange={(v) => setFilters({ ...filters, [f.name]: v })} />
            </div>
          ))}
          <div className="actions">
            <button type="submit" className="auto secondary">Apply</button>
            {Object.values(applied).some((v) => v !== '') && <button type="button" className="auto secondary" onClick={clearFilters}>Clear</button>}
          </div>
        </form>

        {error && <p className="error" role="alert" style={{ margin: 16 }}>{error}</p>}

        <div style={{ overflowX: 'auto' }}>
          <table className="data">
            <thead>
              <tr>
                {def.columns.map((c) => (
                  <th key={c.name} aria-sort={firstSort?.memberName === c.sort ? (firstSort.sortOrder === 1 ? 'ascending' : 'descending') : undefined}>
                    {c.sort ? (
                      <button type="button" onClick={() => toggleSort(c.sort!)}>
                        {c.label}{firstSort?.memberName === c.sort ? (firstSort.sortOrder === 1 ? ' ▲' : ' ▼') : ''}
                      </button>
                    ) : c.label}
                  </th>
                ))}
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={String(row[def.key])}>
                  {def.columns.map((c) => <Cell key={c.name} row={row} column={c} isKey={c.name === def.key} />)}
                  <td><button type="button" className="link" onClick={() => setDrawer({ mode: 'edit', row })}>Edit</button></td>
                </tr>
              ))}
            </tbody>
          </table>
          {!loading && rows.length === 0 && !error && (
            <p className="empty">
              {Object.values(applied).some((v) => v !== '') ? 'Nothing matches these filters.' : `No ${def.title.toLowerCase()} yet. Add the first one.`}
            </p>
          )}
        </div>

        <div className="pager">
          <span>{loading ? 'Loading…' : `${total} ${total === 1 ? 'row' : 'rows'}`}</span>
          <span className="spacer" />
          <button type="button" className="secondary" disabled={page <= 1 || loading} onClick={() => setPage(page - 1)}>Previous</button>
          <span>Page {page} of {pages}</span>
          <button type="button" className="secondary" disabled={page >= pages || loading} onClick={() => setPage(page + 1)}>Next</button>
        </div>
      </section>

      {drawer && (
        <RowDrawer
          def={def}
          row={drawer.mode === 'edit' ? drawer.row : null}
          onClose={() => setDrawer(null)}
          onSaved={() => { setDrawer(null); void load() }}
        />
      )}
    </>
  )
}

function Cell({ row, column, isKey }: { row: Row; column: TableDef['columns'][number]; isKey: boolean }) {
  const value = row[column.name]
  const className = [isKey ? 'key' : '', column.kind === 'number' ? 'num' : ''].filter(Boolean).join(' ') || undefined
  if (column.kind === 'onoff') return <td className={className}><span className={value ? 'pill on' : 'pill off'}>{value ? 'Active' : 'Inactive'}</span></td>
  if (column.kind === 'bool') return <td className={className}>{value ? <span className="pill yes">Yes</span> : <span className="pill off">No</span>}</td>
  if (column.kind === 'system') return <td className={className}>{value ? <span className="pill sys">System</span> : ''}</td>
  if (column.kind === 'rtl') return <td className={className}><span className="ar" lang="ar">{value == null ? '' : String(value)}</span></td>
  return <td className={className}>{value == null ? '' : String(value)}</td>
}

/** Options for a lookup field: up to 100 rows of another table, narrowed by a sibling value when the table asks (subdivision by country). */
function useLookup(field: Field, values: Record<string, string>) {
  const table = field.lookup ? tables.find((t) => t.slug === field.lookup!.table) : undefined
  const narrow = field.lookup?.narrowBy ? values[field.lookup.narrowBy] ?? '' : ''
  const [options, setOptions] = useState<{ value: string; label: string }[]>([])
  useEffect(() => {
    if (!table) return
    if (field.lookup?.narrowBy && !narrow) {
      setOptions([])
      return
    }
    let alive = true
    const body: Record<string, unknown> = { pageNumber: 1, pageSize: 100, sortOrder: table.defaultSort }
    if (field.lookup?.narrowBy) body[field.lookup.narrowBy] = narrow
    void listRows<Row>(table.api, body).then((r) => {
      if (!alive) return
      const rows = r.ok && r.data ? r.data.results : []
      setOptions(rows.map((row) => ({ value: String(row[table.key]), label: `${String(row[table.key])} · ${String(row.nameEn ?? '')}` })))
    })
    return () => { alive = false }
  }, [table, field.lookup?.narrowBy, narrow])
  return options
}

function FilterInput({ field, id, value, values, onChange }: { field: Field; id: string; value: string; values: Record<string, string>; onChange: (v: string) => void }) {
  const options = useLookup(field, values)
  if (field.kind === 'select') {
    return (
      <select id={id} value={value} onChange={(e) => onChange(e.target.value)}>
        {(field.options ?? []).map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    )
  }
  if (field.kind === 'lookup') {
    return (
      <select id={id} value={value} onChange={(e) => onChange(e.target.value)}>
        <option value="">All</option>
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    )
  }
  return <input id={id} value={value} placeholder={field.placeholder} onChange={(e) => onChange(e.target.value)} />
}

function RowDrawer({ def, row, onClose, onSaved }: { def: TableDef; row: Row | null; onClose: () => void; onSaved: () => void }) {
  const editing = row !== null
  const initial = useMemo(() => {
    const v: Record<string, string> = {}
    for (const f of def.fields) {
      const current = row ? row[f.name] : undefined
      if (f.kind === 'bool') v[f.name] = current == null ? (f.name === 'isActive' ? 'true' : 'false') : String(Boolean(current))
      else v[f.name] = current == null ? '' : String(current)
    }
    return v
  }, [def, row])
  const [values, setValues] = useState<Record<string, string>>(initial)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  const visible = def.fields.filter((f) => (editing ? !f.createOnly : !f.editOnly))

  function set(name: string, value: string) {
    setValues((v) => {
      const next = { ...v, [name]: value }
      // a new country empties the subdivision that belonged to the old one
      for (const f of def.fields) if (f.lookup?.narrowBy === name) next[f.name] = ''
      return next
    })
  }

  function valueOf(f: Field): unknown {
    const raw = values[f.name] ?? ''
    if (f.kind === 'bool') return raw === 'true'
    if (f.kind === 'number') {
      if (raw.trim() === '') return f.optional ? null : 0
      return f.decimal ? Number(raw) : Math.trunc(Number(raw))
    }
    const text = raw.trim()
    return text === '' && f.optional ? null : text
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    let result
    if (editing) {
      const body: Record<string, unknown> = { [def.updateKey]: row![def.key] }
      for (const f of def.fields) if (!f.immutable && !f.createOnly) body[f.name] = valueOf(f)
      result = await updateRow(def.api, body)
    } else {
      const body: Record<string, unknown> = {}
      for (const f of def.fields) if (!f.editOnly) body[f.name] = valueOf(f)
      result = await createRow(def.api, body)
    }
    setBusy(false)
    if (!result.ok) {
      setError(messagesFor(result.codes))
      return
    }
    toast(editing ? `${cap(def.singular)} saved.` : `${cap(def.singular)} added${typeof result.data === 'string' && result.data.length <= 32 ? `: ${result.data}` : ''}.`)
    onSaved()
  }

  return (
    <>
      <div className="scrim" onClick={onClose} />
      <form className="drawer" role="dialog" aria-modal="true" aria-labelledby="drawer-title" onSubmit={onSubmit}>
        <header>
          <h2 id="drawer-title">{editing ? `Edit ${def.singular}` : `Add ${def.singular}`}</h2>
          <p className="lead">{editing ? 'The identity of the row (its code or key) cannot change: other rows point at it.' : 'The code or key is chosen once and then fixed.'}</p>
        </header>
        <div className="fields">
          {visible.map((f, index) => (
            <FormField key={f.name} field={f} id={`d-${f.name}`} value={values[f.name] ?? ''} values={values} readOnly={editing && Boolean(f.immutable)} first={index === 0} onChange={(v) => set(f.name, v)} />
          ))}
          {error && <p className="error" role="alert">{error}</p>}
        </div>
        <footer>
          <button type="submit" disabled={busy}>{editing ? 'Save' : 'Add'}</button>
          <button type="button" className="secondary" onClick={onClose}>Cancel</button>
        </footer>
      </form>
    </>
  )
}

function FormField({ field, id, value, values, readOnly, first, onChange }: { field: Field; id: string; value: string; values: Record<string, string>; readOnly: boolean; first: boolean; onChange: (v: string) => void }) {
  const options = useLookup(field, values)
  if (field.kind === 'bool') {
    return (
      <label className="check" htmlFor={id}>
        <input id={id} type="checkbox" checked={value === 'true'} disabled={readOnly} autoFocus={first} onChange={(e) => onChange(e.target.checked ? 'true' : 'false')} />
        <span>{field.label}</span>
      </label>
    )
  }
  const control = (() => {
    if (readOnly) return <input id={id} value={value} readOnly autoFocus={first} />
    if (field.kind === 'select') {
      return (
        <select id={id} value={value} autoFocus={first} onChange={(e) => onChange(e.target.value)}>
          <option value="">Choose…</option>
          {(field.options ?? []).map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      )
    }
    if (field.kind === 'lookup') {
      const narrowed = field.lookup?.narrowBy && !(values[field.lookup.narrowBy] ?? '')
      return (
        <select id={id} value={value} autoFocus={first} onChange={(e) => onChange(e.target.value)} disabled={Boolean(narrowed)}>
          <option value="">{narrowed ? 'Choose the country first' : field.optional ? 'None' : 'Choose…'}</option>
          {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      )
    }
    if (field.kind === 'number') return <input id={id} type="number" step={field.decimal ? 'any' : 1} value={value} autoFocus={first} onChange={(e) => onChange(e.target.value)} />
    return <input id={id} value={value} placeholder={field.placeholder} dir={field.rtl ? 'rtl' : undefined} lang={field.rtl ? 'ar' : undefined} autoFocus={first} onChange={(e) => onChange(e.target.value)} />
  })()
  return (
    <div>
      <label htmlFor={id}>{field.label}{field.optional ? ' (optional)' : ''}</label>
      {control}
      {field.hint && <p className="hint">{field.hint}</p>}
    </div>
  )
}

const cap = (s: string) => s.charAt(0).toUpperCase() + s.slice(1)
