# Inventory Management App

A local portfolio project: an inventory management application for a fictional manufacturing or distribution business.

> **Status:** Sections 0–2 (Foundation, API foundation, and Database) are complete. The API can connect to a local SQL Server running in Docker through EF Core, verified by a `/health/db` check. Inventory data and features are still not implemented: there are no tables, migrations, or inventory endpoints yet. See [Project status](#project-status) and [docs/milestones.md](docs/milestones.md).

## Purpose

Businesses that make or distribute physical goods need to know what they have, where it is, and how long it has been sitting there. This project aims to answer those questions with a small, well-structured application. The app will run entirely on a developer machine, will not be deployed, and will have no external integrations.

## Planned features

- Manage **products** and **warehouses**.
- Track inventory by **dated receipt batches** (each batch records when stock arrived).
- **Receive**, **issue**, **transfer**, and **adjust** stock.
- **Record a movement** for every stock change as each operation is built. Viewing the **movement history** will come later, with the inventory views and alerts.
- Identify **low stock** and **aging stock**.

### Design rules

- Transfers between warehouses will **preserve the original receipt date** of the stock being moved, so aging stays accurate.
- Issuing stock will **not** use FIFO (first in, first out). Users will select which batch to issue from; the app will not choose batches automatically.

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
│   └── Inventory.Api/
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

So far, the documentation and configuration files (Section 0), the solution, API project, and test project (Section 1), and `compose.yaml` with `.env.example` (Section 2) exist. The frontend is planned for a later section. See [SETUP.md](SETUP.md) to start the database and run the API.

## Project status

See the status banner at the top of this file, and [docs/milestones.md](docs/milestones.md) for progress on each section.

## Documentation

- [SETUP.md](SETUP.md): setup and run instructions (database, API, and tests are covered; the frontend is not yet available)
- [docs/architecture.md](docs/architecture.md): how the pieces fit together
- [docs/milestones.md](docs/milestones.md): planned sections and progress
- [docs/project-walkthrough.md](docs/project-walkthrough.md): build log and plain-language explanations
