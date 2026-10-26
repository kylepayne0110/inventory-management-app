import type { ReactNode } from 'react'

export function ErrorBox({ message }: { message: string | null }) {
  return message ? <div className="alert error" role="alert">{message}</div> : null
}

export function Notice({ message }: { message: string | null }) {
  return message ? <div className="alert ok" role="status">{message}</div> : null
}

export function Loading({ loading }: { loading: boolean }) {
  return loading ? <p className="muted">Loading…</p> : null
}

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label className="field"><span>{label}</span>{children}</label>
}

export function Badge({ active }: { active: boolean }) {
  return <span className={`badge ${active ? 'on' : 'off'}`}>{active ? 'Active' : 'Inactive'}</span>
}
