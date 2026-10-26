import { useState } from 'react'
import { api } from '../api'
import { ErrorBox, Loading } from '../components/ui'
import { formatQty } from '../format'
import { useLoad } from '../hooks'

export default function StockLevels() {
  const products = useLoad(api.products, [])
  const warehouses = useLoad(api.warehouses, [])
  const [productId, setProductId] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const levels = useLoad(() => api.stockLevels({ productId, warehouseId }), [productId, warehouseId])

  return (
    <>
      <h1>Stock levels</h1>
      <div className="toolbar">
        <label>Product <select value={productId} onChange={(e) => setProductId(e.target.value)}>
          <option value="">All</option>{products.data?.map((p) => <option key={p.id} value={p.id}>{p.sku}</option>)}</select></label>
        <label>Warehouse <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
          <option value="">All</option>{warehouses.data?.map((w) => <option key={w.id} value={w.id}>{w.code}</option>)}</select></label>
      </div>
      <ErrorBox message={levels.error} />
      <Loading loading={levels.loading && !levels.data} />
      <table>
        <thead><tr><th>SKU</th><th>Product</th><th>Warehouse</th><th className="num">On hand</th><th className="num">Batches</th></tr></thead>
        <tbody>
          {levels.data?.map((l) => (
            <tr key={`${l.productId}-${l.warehouseId}`}>
              <td>{l.productSku}</td><td>{l.productName}</td><td>{l.warehouseCode}</td>
              <td className="num">{formatQty(l.quantityOnHand)}</td><td className="num">{l.batchCount}</td>
            </tr>
          ))}
          {!levels.loading && levels.data?.length === 0 && <tr><td colSpan={5} className="muted">Nothing on hand.</td></tr>}
        </tbody>
      </table>
    </>
  )
}
