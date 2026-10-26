import { useState, type FormEvent } from 'react'
import { api } from '../api'
import { ErrorBox, Field, Loading, Notice } from '../components/ui'
import { formatDate, formatQty, optional } from '../format'
import { useLoad } from '../hooks'
import type { ReceiptBatch, Warehouse } from '../types'

type Action = { kind: 'issue' | 'adjust' | 'transfer'; batch: ReceiptBatch }

// Batches are where stock lives. Issue, adjust and transfer all act on a batch the user picks (no automatic FIFO).
export default function Batches() {
  const products = useLoad(api.products, [])
  const warehouses = useLoad(api.warehouses, [])
  const [productId, setProductId] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const [hideEmpty, setHideEmpty] = useState(true)
  const batches = useLoad(() => api.batches({ productId, warehouseId }), [productId, warehouseId])
  const [action, setAction] = useState<Action | null>(null)
  const [done, setDone] = useState<string | null>(null)

  const sku = (id: number) => products.data?.find((p) => p.id === id)?.sku ?? `#${id}`
  const unit = (id: number) => products.data?.find((p) => p.id === id)?.unitOfMeasure ?? ''
  const code = (id: number) => warehouses.data?.find((w) => w.id === id)?.code ?? `#${id}`
  const rows = (batches.data ?? []).filter((b) => !hideEmpty || b.quantityOnHand > 0)

  return (
    <>
      <h1>Batches</h1>
      <p className="muted">Issue, adjust, or transfer stock from a specific batch. Transfers keep the batch's original received date.</p>
      <div className="toolbar">
        <label>Product <select value={productId} onChange={(e) => setProductId(e.target.value)}>
          <option value="">All</option>{products.data?.map((p) => <option key={p.id} value={p.id}>{p.sku}</option>)}</select></label>
        <label>Warehouse <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
          <option value="">All</option>{warehouses.data?.map((w) => <option key={w.id} value={w.id}>{w.code}</option>)}</select></label>
        <label><input type="checkbox" checked={hideEmpty} onChange={(e) => setHideEmpty(e.target.checked)} /> Hide empty batches</label>
      </div>
      <Notice message={done} />
      <ErrorBox message={batches.error ?? products.error ?? warehouses.error} />
      <Loading loading={batches.loading && !batches.data} />
      {action && (
        <ActionForm
          key={`${action.kind}-${action.batch.id}`}
          action={action}
          sku={sku(action.batch.productId)}
          warehouses={warehouses.data ?? []}
          onClose={() => setAction(null)}
          onDone={(msg) => { setAction(null); setDone(msg); batches.reload() }}
        />
      )}
      <table>
        <thead><tr><th>Batch</th><th>Product</th><th>Warehouse</th><th>Received</th><th>Reference</th><th className="num">Received qty</th><th className="num">On hand</th><th /></tr></thead>
        <tbody>
          {rows.map((b) => (
            <tr key={b.id}>
              <td>#{b.id}</td><td>{sku(b.productId)}</td><td>{code(b.warehouseId)}</td>
              <td>{formatDate(b.receivedAt)}</td><td>{b.reference}</td>
              <td className="num">{formatQty(b.quantityReceived)}</td>
              <td className="num">{formatQty(b.quantityOnHand)} {unit(b.productId)}</td>
              <td className="actions">
                <button className="link" disabled={b.quantityOnHand <= 0} onClick={() => { setDone(null); setAction({ kind: 'issue', batch: b }) }}>Issue</button>
                <button className="link" onClick={() => { setDone(null); setAction({ kind: 'adjust', batch: b }) }}>Adjust</button>
                <button className="link" disabled={b.quantityOnHand <= 0} onClick={() => { setDone(null); setAction({ kind: 'transfer', batch: b }) }}>Transfer</button>
              </td>
            </tr>
          ))}
          {!batches.loading && rows.length === 0 && <tr><td colSpan={8} className="muted">No batches. Receive stock to create one.</td></tr>}
        </tbody>
      </table>
    </>
  )
}

function ActionForm({ action, sku, warehouses, onClose, onDone }: {
  action: Action; sku: string; warehouses: Warehouse[]; onClose: () => void; onDone: (message: string) => void
}) {
  const { kind, batch } = action
  const [quantity, setQuantity] = useState('')
  const [reason, setReason] = useState('')
  const [reference, setReference] = useState('')
  const [destination, setDestination] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      if (kind === 'issue') {
        await api.issue({ receiptBatchId: batch.id, quantity: Number(quantity), reference: optional(reference) })
        onDone(`Issued ${quantity} from batch #${batch.id}.`)
      } else if (kind === 'adjust') {
        await api.adjust({ receiptBatchId: batch.id, quantityChange: Number(quantity), reason: reason.trim(), reference: optional(reference) })
        onDone(`Adjusted batch #${batch.id} by ${quantity}.`)
      } else {
        await api.transfer({ sourceReceiptBatchId: batch.id, destinationWarehouseId: Number(destination), quantity: Number(quantity), reference: optional(reference) })
        onDone(`Transferred ${quantity} from batch #${batch.id}; the new batch keeps its original received date.`)
      }
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const title = { issue: 'Issue', adjust: 'Adjust', transfer: 'Transfer' }[kind]
  return (
    <form className="card" onSubmit={submit} aria-label={`${title} stock`}>
      <h2>{title} — batch #{batch.id} ({sku}, {formatQty(batch.quantityOnHand)} on hand)</h2>
      <div className="grid">
        {kind === 'transfer' && (
          <Field label="Destination warehouse">
            <select required value={destination} onChange={(e) => setDestination(e.target.value)}>
              <option value="">Select…</option>
              {warehouses.filter((w) => w.isActive && w.id !== batch.warehouseId).map((w) => <option key={w.id} value={w.id}>{w.code} — {w.name}</option>)}
            </select>
          </Field>
        )}
        <Field label={kind === 'adjust' ? 'Quantity change (+ or −)' : 'Quantity'}>
          <input required type="number" step="any" {...(kind === 'adjust' ? {} : { min: '0.001' })} value={quantity} onChange={(e) => setQuantity(e.target.value)} />
        </Field>
        {kind === 'adjust' && <Field label="Reason"><input required maxLength={200} value={reason} onChange={(e) => setReason(e.target.value)} /></Field>}
        <Field label="Reference (optional)"><input maxLength={100} value={reference} onChange={(e) => setReference(e.target.value)} /></Field>
      </div>
      <ErrorBox message={error} />
      <button type="submit">{title}</button> <button type="button" className="secondary" onClick={onClose}>Cancel</button>
    </form>
  )
}
