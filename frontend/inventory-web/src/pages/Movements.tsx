import { useState } from 'react'
import { api } from '../api'
import { ErrorBox, Loading } from '../components/ui'
import { formatDateTime, formatQty } from '../format'
import { useLoad } from '../hooks'

const TYPES = ['Receive', 'Issue', 'Adjust', 'TransferOut', 'TransferIn']
const PAGE = 25

export default function Movements() {
  const products = useLoad(api.products, [])
  const warehouses = useLoad(api.warehouses, [])
  const [productId, setProductId] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const [type, setType] = useState('')
  const [skip, setSkip] = useState(0)
  const page = useLoad(
    () => api.movements({ productId, warehouseId, type, skip, take: PAGE }),
    [productId, warehouseId, type, skip],
  )
  const filter = (set: (v: string) => void) => (e: { target: { value: string } }) => { set(e.target.value); setSkip(0) }
  const total = page.data?.total ?? 0

  return (
    <>
      <h1>Movement history</h1>
      <p className="muted">Every receive, issue, adjustment, and transfer, newest first.</p>
      <div className="toolbar">
        <label>Product <select value={productId} onChange={filter(setProductId)}>
          <option value="">All</option>{products.data?.map((p) => <option key={p.id} value={p.id}>{p.sku}</option>)}</select></label>
        <label>Warehouse <select value={warehouseId} onChange={filter(setWarehouseId)}>
          <option value="">All</option>{warehouses.data?.map((w) => <option key={w.id} value={w.id}>{w.code}</option>)}</select></label>
        <label>Type <select value={type} onChange={filter(setType)}>
          <option value="">All</option>{TYPES.map((t) => <option key={t}>{t}</option>)}</select></label>
      </div>
      <ErrorBox message={page.error} />
      <Loading loading={page.loading && !page.data} />
      <table>
        <thead><tr><th>When</th><th>Type</th><th>Product</th><th>Warehouse</th><th>Batch</th><th className="num">Change</th><th>Reason / reference</th></tr></thead>
        <tbody>
          {page.data?.items.map((m) => (
            <tr key={m.id}>
              <td>{formatDateTime(m.occurredAt)}</td>
              <td>{m.type}</td><td>{m.productSku}</td><td>{m.warehouseCode}</td><td>#{m.receiptBatchId}</td>
              <td className={`num ${m.quantityChange < 0 ? 'neg' : 'pos'}`}>{m.quantityChange > 0 ? '+' : ''}{formatQty(m.quantityChange)}</td>
              <td>{[m.reason, m.reference].filter(Boolean).join(' · ')}{m.transferId && <div className="muted small">transfer {m.transferId.slice(0, 8)}</div>}</td>
            </tr>
          ))}
          {!page.loading && total === 0 && <tr><td colSpan={7} className="muted">No movements.</td></tr>}
        </tbody>
      </table>
      <div className="toolbar">
        <button className="secondary" disabled={skip === 0} onClick={() => setSkip(Math.max(0, skip - PAGE))}>Previous</button>
        <span className="muted">{total === 0 ? 0 : skip + 1}–{Math.min(skip + PAGE, total)} of {total}</span>
        <button className="secondary" disabled={skip + PAGE >= total} onClick={() => setSkip(skip + PAGE)}>Next</button>
      </div>
    </>
  )
}
