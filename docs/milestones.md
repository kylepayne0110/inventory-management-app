# Project Sections

This file tracks the planned sections of the build and their progress.

**Status:** Sections 0 to 7 are complete. Section 3, Products and warehouses: products and warehouses can be created, listed, viewed, updated, and deactivated through the API. Section 4, Receipt batches and receiving stock, is complete: stock can be received into a warehouse as a batch, and each receipt records a movement. Section 5, Issuing and adjusting stock, is complete: stock can be issued from a chosen batch or adjusted with a reason, and each operation records a movement. Section 6, Transfers, is complete: stock can be transferred to another warehouse, keeping its original receipt date and recording two movements. Section 7, Inventory views and alerts, is complete: movement history, stock levels, low-stock alerts, and aging-stock alerts can be read through the API (read-only; adds a per-product reorder threshold with one additive migration). Sections 8 onward are not started.

Status key: ⏳ in progress · ⬜ not started · ✅ done

The list below is the current plan and may be refined as the project proceeds.

| # | Section | Status |
| --- | --- | --- |
| 0 | **Foundation**: README, setup outline, architecture and walkthrough docs, `.gitignore`, `.editorconfig` | ✅ Done |
| 1 | **API foundation**: .NET solution (`InventoryManagement.slnx`), minimal API with a health endpoint and OpenAPI/Swagger docs, xUnit test project | ✅ Done |
| 2 | **Database**: SQL Server in Docker via `compose.yaml` with a named data volume, EF Core setup (`InventoryDbContext`, `/health/db`); no migration yet | ✅ Done |
| 3 | **Products and warehouses**: `Product` and `Warehouse` tables with unique SKU and code, validated create/list/get/update/deactivate endpoints (no deleting), and the first EF Core migration, `InitialCreate` | ✅ Done |
| 4 | **Receipt batches and receiving stock**, recording a movement for each receipt: `ReceiptBatch` and `StockMovement` tables, receive/list/get endpoints, and the `AddReceiptBatchesAndStockMovements` migration | ✅ Done |
| 5 | **Issuing and adjusting stock**: users select the batch to issue from (no automatic FIFO); each operation records a movement; adds `Reason` on movements and a concurrency `RowVersion` on batches (the `AddStockMovementReasonAndBatchRowVersion` migration) | ✅ Done |
| 6 | **Transfers**, preserving original receipt dates and recording movements: `POST /api/stock/transfer`, `TransferOut` and `TransferIn` movement types; a shared `TransferId` on the two movements (the `AddStockMovementTransferId` migration) | ✅ Done |
| 7 | **Inventory views and alerts**: movement-history viewing, low-stock and aging-stock identification: read-only `GET /api/stock-movements`, `/api/inventory/stock-levels`, `/api/inventory/alerts/low-stock`, `/api/inventory/alerts/aging-stock`; adds `Product.ReorderThreshold` (the `AddProductReorderThreshold` migration) | ✅ Done |
| 8 | **React frontend**: scaffolding and screens for each feature | ⬜ Not started |
| 9 | **Testing and polish**: broader xUnit coverage, final documentation, completed SETUP.md | ⬜ Not started |

Movements are recorded as each inventory operation is implemented (sections 4 to 6). Viewing movement history was added in section 7.

Each section ends with an update to [project-walkthrough.md](project-walkthrough.md).
