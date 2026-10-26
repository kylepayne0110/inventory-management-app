import { useState } from 'react'
import { api } from '../api'
import { ErrorBox, Loading } from '../components/ui'
import { formatDate, formatQty } from '../format'
import { useLoad } from '../hooks'

export default function Alerts() {
  const warehouses = useLoad(api.warehouses, [])
  const [warehouseId, setWarehouseId] = useState('')
  const [days, setDays] = useState('90')
  const daysNumber = Number(days)
  const daysValid = days !== '' && Number.isInteger(daysNumber) && daysNumber >= 0 && daysNumber <= 36500
  const low = useLoad(() => api.lowStock(warehouseId), [warehouseId])
  const aging = useLoad(() => (daysValid ? api.agingStock(daysNumber) : Promise.resolve([])), [daysValid, daysNumber])

  return (
    <>
      <h1>Alerts</h1>
      <section>
        <h2>Low stock <span className="count">{low.data?.length ?? 0}</span></h2>
        <p className="muted">Active products at or below their own reorder threshold. A threshold of 0 alerts only when a product is out of stock.</p>
        <div className="toolbar">
          <label>Count stock in <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
            <option value="">All warehouses</option>{warehouses.data?.map((w) => <option key={w.id} value={w.id}>{w.code}</option>)}</select></label>
        </div>
        <ErrorBox message={low.error} />
        <Loading loading={low.loading && !low.data} />
        <table>
          <thead><tr><th>SKU</th><th>Product</th><th className="num">On hand</th><th className="num">Reorder at</th></tr></thead>
          <tbody>
            {low.data?.map((a) => (
              <tr key={a.productId}><td>{a.productSku}</td><td>{a.productName}</td>
                <td className="num neg">{formatQty(a.quantityOnHand)}</td><td className="num">{formatQty(a.threshold)}</td></tr>
            ))}
            {!low.loading && low.data?.length === 0 && <tr><td colSpan={4} className="muted">No low-stock products.</td></tr>}
          </tbody>
        </table>
      </section>
      <section>
        <h2>Aging stock <span className="count">{aging.data?.length ?? 0}</span></h2>
        <p className="muted">Batches still holding stock that were received at least this many days ago. Transferred stock keeps its original age.</p>
        <div className="toolbar">
          <label>Days <input type="number" min="0" max="36500" step="1" value={days} onChange={(e) => setDays(e.target.value)} /></label>
          {!daysValid && <span className="neg">Enter a whole number from 0 to 36500.</span>}
        </div>
        <ErrorBox message={aging.error} />
        <table>
          <thead><tr><th>Batch</th><th>SKU</th><th>Warehouse</th><th>Received</th><th className="num">Age (days)</th><th className="num">On hand</th></tr></thead>
          <tbody>
            {aging.data?.map((a) => (
              <tr key={a.receiptBatchId}><td>#{a.receiptBatchId}</td><td>{a.productSku}</td><td>{a.warehouseCode}</td>
                <td>{formatDate(a.receivedAt)}</td><td className="num">{a.ageDays}</td><td className="num">{formatQty(a.quantityOnHand)}</td></tr>
            ))}
            {!aging.loading && aging.data?.length === 0 && <tr><td colSpan={6} className="muted">No aging stock.</td></tr>}
          </tbody>
        </table>
      </section>
    </>
  )
}
