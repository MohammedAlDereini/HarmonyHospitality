import { useSyncExternalStore } from 'react'

/** Short confirmations at the bottom of the screen. One module-level store, so any page can speak without plumbing. */
type Toast = { id: number; text: string; bad: boolean }

let toasts: Toast[] = []
let nextId = 1
const listeners = new Set<() => void>()

function emit() {
  for (const l of listeners) l()
}

export function toast(text: string, bad = false): void {
  const id = nextId++
  toasts = [...toasts, { id, text, bad }]
  emit()
  setTimeout(() => {
    toasts = toasts.filter((t) => t.id !== id)
    emit()
  }, bad ? 6000 : 3500)
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function ToastHost() {
  const list = useSyncExternalStore(subscribe, () => toasts, () => toasts)
  if (list.length === 0) return null
  return (
    <div className="toasts" role="status" aria-live="polite">
      {list.map((t) => (
        <div key={t.id} className={t.bad ? 'toast bad' : 'toast'}>{t.text}</div>
      ))}
    </div>
  )
}
