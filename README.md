# Inventory Management App

A local portfolio project: an inventory management application for a fictional manufacturing or distribution business.

> **Status:** Sections 0–5 (Foundation, API foundation, Database, Products and warehouses, Receiving stock, and Issuing and adjusting stock) are complete. The API can manage products and warehouses, receive stock into dated batches, and issue or adjust stock from a chosen batch, recording a movement each time. Transfers, movement-history viewing, alerts, and the frontend are not started. See [Project status](#project-status) and [docs/milestones.md](docs/milestones.md).

## Purpose

Businesses that make or distribute physical goods need to know what they have, where it is, and how long it has been sitting there. This project aims to answer those questions with a small, well-structured application. The app will run entirely on a developer machine, will not be deployed, and will have no external integrations.

## Planned features

- Manage **products** and **warehouses** (available now: `/api/products` and `/api/warehouses`; records are deactivated, never deleted).
- Track inventory by **dated receipt batches** (each batch records when stock arrived).
- **Receive**, **issue**, and **adjust** stock (available now); **transfer** stock (planned).
- **Record a movement** for every stock change as each operation is built. Viewing the **movement history** will come later, with the inventory views and alerts.
- Identify **low stock** and **aging stock**.

### Design rules

- Transfers between warehouses will **preserve the original receipt date** of the stock being moved, so aging stays accurate.
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

So far, the documentation and configuration files (Section 0), the solution, API project, and test project (Section 1), `compose.yaml` with `.env.example` (Section 2), and the product and warehouse models, endpoints, and first migration (Section 3), receipt batches and receiving (Section 4), and issue and adjust endpoints (Section 5) exist. The frontend is planned for a later section. See [SETUP.md](SETUP.md) to start the database and run the API.

## Products and warehouses API (Section 3)

| Method and path | What it does |
| --- | --- |
| `POST /api/products` | Create a product (`sku`, `name`, optional `description`, optional `unitOfMeasure`, default `each`) |
| `GET /api/products` | List products; add `?isActive=true` or `?isActive=false` to filter |
| `GET /api/products/{id}` | Get one product |
| `PUT /api/products/{id}` | Update name, description, and unit of measure (the SKU cannot change) |
| `POST /api/products/{id}/deactivate` | Mark a product inactive |

Warehouses work the same way under `/api/warehouses`, with `code`, `name`, and optional `location`. There is no delete endpoint for either: deactivating keeps the record so future inventory history stays intact. SKUs and warehouse codes are unique, ignoring upper/lower case. See [docs/project-walkthrough.md](docs/project-walkthrough.md) for details.

## Receiving, issuing, and adjusting stock (Sections 4 and 5)

| Method and path | What it does |
| --- | --- |
| `POST /api/receipt-batches` | Receive stock into a warehouse as a new dated batch |
| `GET /api/receipt-batches` | List batches (filter with `?productId=` and `?warehouseId=`) |
| `GET /api/receipt-batches/{id}` | Get one batch |
| `POST /api/stock/issue` | Issue a quantity from a chosen batch (cannot exceed what is on hand) |
| `POST /api/stock/adjust` | Correct a batch up or down with a required reason |

Every receive, issue, and adjustment records a stock movement. See [docs/project-walkthrough.md](docs/project-walkthrough.md) for the rules.

## Project status

See the status banner at the top of this file, and [docs/milestones.md](docs/milestones.md) for progress on each section.

## Documentation

- [SETUP.md](SETUP.md): setup and run instructions (database, API, and tests are covered; the frontend is not yet available)
- [docs/architecture.md](docs/architecture.md): how the pieces fit together
- [docs/milestones.md](docs/milestones.md): planned sections and progress
- [docs/project-walkthrough.md](docs/project-walkthrough.md): build log and plain-language explanations
