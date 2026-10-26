import { useState, type FormEvent } from 'react'
import { api } from '../api'
import { Badge, ErrorBox, Field, Loading } from '../components/ui'
import { optional } from '../format'
import { useLoad } from '../hooks'
import type { Warehouse } from '../types'

export default function Warehouses() {
  const { data, error, loading, reload } = useLoad(api.warehouses, [])
  const [editing, setEditing] = useState<Warehouse | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [form, setForm] = useState({ code: '', name: '', location: '' })

  async function create(e: FormEvent) {
    e.preventDefault()
    setFormError(null)
    try {
      await api.createWarehouse({ code: form.code.trim(), name: form.name.trim(), location: optional(form.location) })
      setForm({ code: '', name: '', location: '' })
      reload()
    } catch (err) {
      setFormError((err as Error).message)
    }
  }

  async function deactivate(w: Warehouse) {
    if (!confirm(`Deactivate ${w.code}? It keeps its history but cannot receive new stock.`)) return
    try {
      await api.deactivateWarehouse(w.id)
      reload()
    } catch (err) {
      setFormError((err as Error).message)
    }
  }

  return (
    <>
      <h1>Warehouses</h1>
      <form className="card" onSubmit={create} aria-label="New warehouse">
        <h2>New warehouse</h2>
        <div className="grid">
          <Field label="Code"><input required maxLength={20} value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} /></Field>
          <Field label="Name"><input required maxLength={200} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
          <Field label="Location"><input maxLength={500} value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} /></Field>
        </div>
        <button type="submit">Create warehouse</button>
      </form>
      <ErrorBox message={formError ?? error} />
      <Loading loading={loading && !data} />
      <table>
        <thead><tr><th>Code</th><th>Name</th><th>Location</th><th>Status</th><th /></tr></thead>
        <tbody>
          {(data ?? []).map((w) => (
            <tr key={w.id}>
              <td>{w.code}</td><td>{w.name}</td><td>{w.location}</td>
              <td><Badge active={w.isActive} /></td>
              <td className="actions">
                <button className="link" onClick={() => setEditing(w)}>Edit</button>
                {w.isActive && <button className="link danger" onClick={() => deactivate(w)}>Deactivate</button>}
              </td>
            </tr>
          ))}
          {!loading && data?.length === 0 && <tr><td colSpan={5} className="muted">No warehouses yet.</td></tr>}
        </tbody>
      </table>
      {editing && <EditWarehouse key={editing.id} warehouse={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </>
  )
}

function EditWarehouse({ warehouse, onClose, onSaved }: { warehouse: Warehouse; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState({ name: warehouse.name, location: warehouse.location ?? '' })
  const [error, setError] = useState<string | null>(null)

  async function save(e: FormEvent) {
    e.preventDefault()
    try {
      await api.updateWarehouse(warehouse.id, { name: f.name.trim(), location: optional(f.location) })
      onSaved()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <form className="card" onSubmit={save} aria-label={`Edit ${warehouse.code}`}>
      <h2>Edit {warehouse.code}</h2>
      <p className="muted small">The code cannot be changed.</p>
      <div className="grid">
        <Field label="Name"><input required value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
        <Field label="Location"><input value={f.location} onChange={(e) => setF({ ...f, location: e.target.value })} /></Field>
      </div>
      <ErrorBox message={error} />
      <button type="submit">Save</button> <button type="button" className="secondary" onClick={onClose}>Cancel</button>
    </form>
  )
}
