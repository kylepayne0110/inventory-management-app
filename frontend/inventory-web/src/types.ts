export type MovementType = 'Receive' | 'Issue' | 'Adjust' | 'TransferOut' | 'TransferIn'

export interface Product {
  id: number
  sku: string
  name: string
  description: string | null
  unitOfMeasure: string
  reorderThreshold: number
  isActive: boolean
}

export interface Warehouse {
  id: number
  code: string
  name: string
  location: string | null
  isActive: boolean
}

export interface ReceiptBatch {
  id: number
  productId: number
  warehouseId: number
  receivedAt: string
  quantityReceived: number
  quantityOnHand: number
  reference: string | null
}

export interface StockMovement {
  id: number
  type: MovementType
  productId: number
  productSku: string
  warehouseId: number
  warehouseCode: string
  receiptBatchId: number
  quantityChange: number
  occurredAt: string
  reference: string | null
  reason: string | null
  transferId: string | null
}

export interface MovementPage {
  total: number
  skip: number
  take: number
  items: StockMovement[]
}

export interface StockLevel {
  productId: number
  productSku: string
  productName: string
  warehouseId: number
  warehouseCode: string
  quantityOnHand: number
  batchCount: number
}

export interface LowStockAlert {
  productId: number
  productSku: string
  productName: string
  quantityOnHand: number
  threshold: number
}

export interface AgingStockAlert {
  receiptBatchId: number
  productSku: string
  warehouseCode: string
  receivedAt: string
  ageDays: number
  quantityOnHand: number
  reference: string | null
}
