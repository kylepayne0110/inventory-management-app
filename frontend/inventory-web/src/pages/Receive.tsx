import { useState, type FormEvent } from 'react'
import { api } from '../api'
import { ErrorBox, Field, Notice } from '../components/ui'
import { optional } from '../format'
import { useLoad } from '../hooks'

// Latest date the API accepts for a receipt (it rejects future dates).
const today = new Date().toLocaleDateString('en-CA')

export default function Receive() {
  const products = useLoad(api.products, [])
  const warehouses = useLoad(api.warehouses, [])
  const [form, setForm] = useState({ productId: '', warehouseId: '', quantity: '', receivedAt: '', reference: '' })
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setDone(null)
    try {
      const batch = await api.receive({
        productId: Number(form.productId),
        warehouseId: Number(form.warehouseId),
        quantity: Number(form.quantity),
        // Midnight local time on the chosen day; the API rejects future dates.
        receivedAt: form.receivedAt ? new Date(`${form.receivedAt}T00:00:00`).toISOString() : undefined,
        reference: optional(form.reference),
      })
      setDone(`Received into new batch #${batch.id}.`)
      setForm({ ...form, quantity: '', reference: '' })
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <>
      <h1>Receive stock</h1>
      <p className="muted">Each receipt creates a new dated batch, so the age of every piece of stock is known.</p>
      <form className="card" onSubmit={submit} aria-label="Receive stock">
        <div className="grid">
          <Field label="Product">
            <select required value={form.productId} onChange={(e) => setForm({ ...form, productId: e.target.value })}>
              <option value="">Select…</option>
              {products.data?.filter((p) => p.isActive).map((p) => <option key={p.id} value={p.id}>{p.sku} — {p.name}</option>)}
            </select>
          </Field>
          <Field label="Warehouse">
            <select required value={form.warehouseId} onChange={(e) => setForm({ ...form, warehouseId: e.target.value })}>
              <option value="">Select…</option>
              {warehouses.data?.filter((w) => w.isActive).map((w) => <option key={w.id} value={w.id}>{w.code} — {w.name}</option>)}
            </select>
          </Field>
          <Field label="Quantity"><input required type="number" min="0.001" step="any" value={form.quantity} onChange={(e) => setForm({ ...form, quantity: e.target.value })} /></Field>
          <Field label="Received date (optional)"><input type="date" max={today} value={form.receivedAt} onChange={(e) => setForm({ ...form, receivedAt: e.target.value })} /></Field>
          <Field label="Reference (optional)"><input maxLength={100} placeholder="e.g. PO number" value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} /></Field>
        </div>
        <ErrorBox message={error ?? products.error ?? warehouses.error} />
        <Notice message={done} />
        <button type="submit">Receive</button>
      </form>
    </>
  )
}
