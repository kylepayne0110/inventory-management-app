import { useState, type FormEvent } from 'react'
import { api } from '../api'
import { Badge, ErrorBox, Field, Loading } from '../components/ui'
import { formatQty, optional } from '../format'
import { useLoad } from '../hooks'
import type { Product } from '../types'

export default function Products() {
  const { data, error, loading, reload } = useLoad(api.products, [])
  const [showInactive, setShowInactive] = useState(true)
  const [editing, setEditing] = useState<Product | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [form, setForm] = useState({ sku: '', name: '', description: '', unitOfMeasure: '', reorderThreshold: '' })

  async function create(e: FormEvent) {
    e.preventDefault()
    setFormError(null)
    try {
      await api.createProduct({
        sku: form.sku.trim(),
        name: form.name.trim(),
        description: optional(form.description),
        unitOfMeasure: optional(form.unitOfMeasure),
        reorderThreshold: form.reorderThreshold === '' ? undefined : Number(form.reorderThreshold),
      })
      setForm({ sku: '', name: '', description: '', unitOfMeasure: '', reorderThreshold: '' })
      reload()
    } catch (err) {
      setFormError((err as Error).message)
    }
  }

  async function deactivate(p: Product) {
    if (!confirm(`Deactivate ${p.sku}? It keeps its history but cannot receive new stock.`)) return
    try {
      await api.deactivateProduct(p.id)
      reload()
    } catch (err) {
      setFormError((err as Error).message)
    }
  }

  const rows = (data ?? []).filter((p) => showInactive || p.isActive)

  return (
    <>
      <h1>Products</h1>
      <form className="card" onSubmit={create} aria-label="New product">
        <h2>New product</h2>
        <div className="grid">
          <Field label="SKU"><input required maxLength={50} value={form.sku} onChange={(e) => setForm({ ...form, sku: e.target.value })} /></Field>
          <Field label="Name"><input required maxLength={200} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
          <Field label="Unit of measure"><input maxLength={20} placeholder="each" value={form.unitOfMeasure} onChange={(e) => setForm({ ...form, unitOfMeasure: e.target.value })} /></Field>
          <Field label="Reorder threshold"><input type="number" min="0" step="any" placeholder="0" value={form.reorderThreshold} onChange={(e) => setForm({ ...form, reorderThreshold: e.target.value })} /></Field>
          <Field label="Description"><input maxLength={1000} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field>
        </div>
        <button type="submit">Create product</button>
      </form>
      <ErrorBox message={formError ?? error} />
      <div className="toolbar">
        <label><input type="checkbox" checked={showInactive} onChange={(e) => setShowInactive(e.target.checked)} /> Show inactive</label>
      </div>
      <Loading loading={loading && !data} />
      <table>
        <thead><tr><th>SKU</th><th>Name</th><th>Unit</th><th className="num">Reorder at</th><th>Status</th><th /></tr></thead>
        <tbody>
          {rows.map((p) => (
            <tr key={p.id}>
              <td>{p.sku}</td>
              <td>{p.name}{p.description && <div className="muted small">{p.description}</div>}</td>
              <td>{p.unitOfMeasure}</td>
              <td className="num">{formatQty(p.reorderThreshold)}</td>
              <td><Badge active={p.isActive} /></td>
              <td className="actions">
                <button className="link" onClick={() => setEditing(p)}>Edit</button>
                {p.isActive && <button className="link danger" onClick={() => deactivate(p)}>Deactivate</button>}
              </td>
            </tr>
          ))}
          {!loading && rows.length === 0 && <tr><td colSpan={6} className="muted">No products yet.</td></tr>}
        </tbody>
      </table>
      {editing && <EditProduct key={editing.id} product={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </>
  )
}

function EditProduct({ product, onClose, onSaved }: { product: Product; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState({
    name: product.name,
    description: product.description ?? '',
    unitOfMeasure: product.unitOfMeasure,
    reorderThreshold: String(product.reorderThreshold),
  })
  const [error, setError] = useState<string | null>(null)

  async function save(e: FormEvent) {
    e.preventDefault()
    try {
      await api.updateProduct(product.id, {
        name: f.name.trim(),
        description: optional(f.description),
        unitOfMeasure: optional(f.unitOfMeasure),
        reorderThreshold: f.reorderThreshold === '' ? undefined : Number(f.reorderThreshold),
      })
      onSaved()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <form className="card" onSubmit={save} aria-label={`Edit ${product.sku}`}>
      <h2>Edit {product.sku}</h2>
      <p className="muted small">The SKU cannot be changed.</p>
      <div className="grid">
        <Field label="Name"><input required value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
        <Field label="Unit of measure"><input value={f.unitOfMeasure} onChange={(e) => setF({ ...f, unitOfMeasure: e.target.value })} /></Field>
        <Field label="Reorder threshold"><input type="number" min="0" step="any" value={f.reorderThreshold} onChange={(e) => setF({ ...f, reorderThreshold: e.target.value })} /></Field>
        <Field label="Description"><input value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} /></Field>
      </div>
      <ErrorBox message={error} />
      <button type="submit">Save</button> <button type="button" className="secondary" onClick={onClose}>Cancel</button>
    </form>
  )
}
