# Inventory Management App

A local portfolio project: an inventory management application for a fictional manufacturing or distribution business.

> **Status:** Section 0, Foundation, is complete. The documentation and repository foundation are in place, but no application features are implemented. See [Project status](#project-status) and [docs/milestones.md](docs/milestones.md).

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
├── InventoryManagement.slnx
├── README.md
├── SETUP.md
├── .editorconfig
└── .gitignore
```

Only the documentation and configuration files exist so far (the Section 0 foundation). The backend, frontend, test project, solution, and Docker configuration are planned for later sections.

## Project status

See the status banner at the top of this file, and [docs/milestones.md](docs/milestones.md) for progress on each section.

## Documentation

- [SETUP.md](SETUP.md): setup and run instructions (initial outline, to be completed as the project is built)
- [docs/architecture.md](docs/architecture.md): how the pieces fit together
- [docs/milestones.md](docs/milestones.md): planned sections and progress
- [docs/project-walkthrough.md](docs/project-walkthrough.md): build log and plain-language explanations
