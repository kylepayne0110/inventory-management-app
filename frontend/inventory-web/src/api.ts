import type {
  AgingStockAlert, LowStockAlert, MovementPage, Product, ReceiptBatch, StockLevel, Warehouse,
} from './types'

export class ApiError extends Error {
  status: number
  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

// Turns the API's error shapes ({ error } or a validation problem with an errors map) into one readable message.
function messageFrom(body: unknown, status: number): string {
  if (body && typeof body === 'object') {
    const b = body as { error?: string; errors?: Record<string, string[]>; title?: string }
    if (b.error) return b.error
    if (b.errors) return Object.values(b.errors).flat().join(' ')
    if (b.title) return b.title
  }
  return `Request failed (${status}).`
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  let response: Response
  try {
    response = await fetch(path, {
      method,
      headers: body === undefined ? undefined : { 'content-type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError('Cannot reach the API. Is it running on http://localhost:5127?', 0)
  }
  const text = await response.text()
  const data = text ? JSON.parse(text) : undefined
  if (!response.ok) throw new ApiError(messageFrom(data, response.status), response.status)
  return data as T
}

const query = (params: Record<string, string | number | boolean | undefined | null>) => {
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') p.set(k, String(v))
  const s = p.toString()
  return s ? `?${s}` : ''
}

export const api = {
  products: () => request<Product[]>('GET', '/api/products'),
  createProduct: (b: { sku: string; name: string; description?: string; unitOfMeasure?: string; reorderThreshold?: number }) =>
    request<Product>('POST', '/api/products', b),
  updateProduct: (id: number, b: { name: string; description?: string; unitOfMeasure?: string; reorderThreshold?: number }) =>
    request<Product>('PUT', `/api/products/${id}`, b),
  deactivateProduct: (id: number) => request<Product>('POST', `/api/products/${id}/deactivate`),

  warehouses: () => request<Warehouse[]>('GET', '/api/warehouses'),
  createWarehouse: (b: { code: string; name: string; location?: string }) => request<Warehouse>('POST', '/api/warehouses', b),
  updateWarehouse: (id: number, b: { name: string; location?: string }) => request<Warehouse>('PUT', `/api/warehouses/${id}`, b),
  deactivateWarehouse: (id: number) => request<Warehouse>('POST', `/api/warehouses/${id}/deactivate`),

  batches: (f: { productId?: string; warehouseId?: string } = {}) => request<ReceiptBatch[]>('GET', `/api/receipt-batches${query(f)}`),
  receive: (b: { productId: number; warehouseId: number; quantity: number; receivedAt?: string; reference?: string }) =>
    request<ReceiptBatch>('POST', '/api/receipt-batches', b),
  issue: (b: { receiptBatchId: number; quantity: number; reference?: string }) => request('POST', '/api/stock/issue', b),
  adjust: (b: { receiptBatchId: number; quantityChange: number; reason: string; reference?: string }) =>
    request('POST', '/api/stock/adjust', b),
  transfer: (b: { sourceReceiptBatchId: number; destinationWarehouseId: number; quantity: number; reference?: string }) =>
    request('POST', '/api/stock/transfer', b),

  movements: (f: Record<string, string | number | undefined>) => request<MovementPage>('GET', `/api/stock-movements${query(f)}`),
  stockLevels: (f: { productId?: string; warehouseId?: string } = {}) =>
    request<StockLevel[]>('GET', `/api/inventory/stock-levels${query(f)}`),
  lowStock: (warehouseId?: string) => request<LowStockAlert[]>('GET', `/api/inventory/alerts/low-stock${query({ warehouseId })}`),
  agingStock: (days: number) => request<AgingStockAlert[]>('GET', `/api/inventory/alerts/aging-stock${query({ days })}`),
}
