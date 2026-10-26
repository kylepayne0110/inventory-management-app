# Inventory Management App

A local portfolio project: an inventory management application for a fictional manufacturing or distribution business.

> **Status:** Sections 0–7 (Foundation, API foundation, Database, Products and warehouses, Receiving stock, Issuing and adjusting stock, Transfers, and Inventory views and alerts) are complete. The API can manage products and warehouses, receive stock into dated batches, and issue or adjust stock from a chosen batch, recording a movement each time. Stock can also be transferred between warehouses, keeping its original receipt date. Movement history, stock levels, and low-stock and aging-stock alerts can be read through the API. The frontend is not started. See [Project status](#project-status) and [docs/milestones.md](docs/milestones.md).

## Purpose

Businesses that make or distribute physical goods need to know what they have, where it is, and how long it has been sitting there. This project aims to answer those questions with a small, well-structured application. The app will run entirely on a developer machine, will not be deployed, and will have no external integrations.

## Planned features

- Manage **products** and **warehouses** (available now: `/api/products` and `/api/warehouses`; records are deactivated, never deleted).
- Track inventory by **dated receipt batches** (each batch records when stock arrived).
- **Receive**, **issue**, **adjust**, and **transfer** stock (available now).
- **Record a movement** for every stock change as each operation is built. Viewing the **movement history** will come later, with the inventory views and alerts.
- Identify **low stock** and **aging stock**.

### Design rules

- Transfers between warehouses **preserve the original receipt date** of the stock being moved, so aging stays accurate.
- Issuing stock does **not** use FIFO (first in, first out). Users select which batch to issue from; the app does not choose batches automatically.

## Planned stack

| Area | Technology |
| --- | --- |
| Backend | C# with a .NET 10 Web API |
| Data access | Entity Framework Core |
| Database | SQL Server, running locally in Docker |
| Frontend | React with TypeScript |
| Tests | xUnit |
| Source control | Git / GitHub |

The app will run locally only, will not be deployed, and will have no external integrations.

## Architecture at a high level

A React app in the browser will call a .NET Web API over HTTP. The API will use Entity Framework Core to read and write a SQL Server database running in a Docker container, with its data kept in a named Docker volume. See [docs/architecture.md](docs/architecture.md) for a diagram and details.

## Planned repository layout

```
inventory-management-app/
├── backend/
│   └── Inventory.Api/   (Models, Contracts, Endpoints, Data, Data/Migrations)
├── frontend/
│   └── inventory-web/
├── tests/
│   └── Inventory.Api.Tests/
├── docs/
│   ├── architecture.md
│   ├── milestones.md
│   └── project-walkthrough.md
├── compose.yaml
├── .env.example
├── InventoryManagement.slnx
├── README.md
├── SETUP.md
├── .editorconfig
└── .gitignore
```

So far, the documentation and configuration files (Section 0), the solution, API project, and test project (Section 1), `compose.yaml` with `.env.example` (Section 2), and the product and warehouse models, endpoints, and first migration (Section 3), receipt batches and receiving (Section 4), issue and adjust endpoints (Section 5), transfers (Section 6), and the read-only inventory views and alerts (Section 7) exist. The frontend is planned for a later section. See [SETUP.md](SETUP.md) to start the database and run the API.

## Products and warehouses API (Section 3)

| Method and path | What it does |
| --- | --- |
| `POST /api/products` | Create a product (`sku`, `name`, optional `description`, optional `unitOfMeasure`, default `each`, optional `reorderThreshold`, default 0) |
| `GET /api/products` | List products; add `?isActive=true` or `?isActive=false` to filter |
| `GET /api/products/{id}` | Get one product |
| `PUT /api/products/{id}` | Update name, description, unit of measure, and reorder threshold (omit the threshold to keep it; the SKU cannot change) |
| `POST /api/products/{id}/deactivate` | Mark a product inactive |

Warehouses work the same way under `/api/warehouses`, with `code`, `name`, and optional `location`. There is no delete endpoint for either: deactivating keeps the record so future inventory history stays intact. SKUs and warehouse codes are unique, ignoring upper/lower case. See [docs/project-walkthrough.md](docs/project-walkthrough.md) for details.

## Receiving, issuing, adjusting, and transferring stock (Sections 4 to 6)

| Method and path | What it does |
| --- | --- |
| `POST /api/receipt-batches` | Receive stock into a warehouse as a new dated batch |
| `GET /api/receipt-batches` | List batches (filter with `?productId=` and `?warehouseId=`) |
| `GET /api/receipt-batches/{id}` | Get one batch |
| `POST /api/stock/issue` | Issue a quantity from a chosen batch (cannot exceed what is on hand) |
| `POST /api/stock/adjust` | Correct a batch up or down with a required reason |
| `POST /api/stock/transfer` | Move a quantity from a chosen batch to another warehouse, keeping its original receipt date |

Every receive, issue, and adjustment records a stock movement; a transfer records two movements that share a `TransferId`. See [docs/project-walkthrough.md](docs/project-walkthrough.md) for the rules.

## Inventory views and alerts (Section 7)

All read-only; no data is changed. The low-stock alert uses each product's `reorderThreshold` (0 or more, up to 3 decimal places, set when creating or updating the product). The default of 0 means a product alerts only when it is out of stock.

| Method and path | What it does |
| --- | --- |
| `GET /api/stock-movements` | Movement history, newest first. Filters: `productId`, `warehouseId`, `receiptBatchId`, `type` (`Receive`, `Issue`, `Adjust`, `TransferOut`, `TransferIn`), `transferId`, `from`, `to`; paging with `skip` and `take` (default 100, max 500) |
| `GET /api/inventory/stock-levels` | Quantity on hand per product and warehouse (`productId`, `warehouseId` filters) |
| `GET /api/inventory/alerts/low-stock` | Active products whose total on hand is at or below their own saved `reorderThreshold`, including products with none; `warehouseId` limits the count to one warehouse |
| `GET /api/inventory/alerts/aging-stock` | Batches still holding stock that were received at least `days` ago (default 90), oldest first; `productId` and `warehouseId` filters |

## Project status

See the status banner at the top of this file, and [docs/milestones.md](docs/milestones.md) for progress on each section.

## Documentation

- [SETUP.md](SETUP.md): setup and run instructions (database, API, and tests are covered; the frontend is not yet available)
- [docs/architecture.md](docs/architecture.md): how the pieces fit together
- [docs/milestones.md](docs/milestones.md): planned sections and progress
- [docs/project-walkthrough.md](docs/project-walkthrough.md): build log and plain-language explanations
