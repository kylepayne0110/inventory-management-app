# Project Walkthrough

This is the build log for the inventory management app. It is updated at the end of each section of the project.

Each entry will capture:

- **What we built**: the concrete result of the section.
- **Why**: the reasoning behind it.
- **Important commands and decisions**: what was run, and what alternatives were considered.
- **Plain-language explanations**: short, non-jargon descriptions of the technologies involved, so a nontechnical reader can follow along.

See [milestones.md](milestones.md) for the list of sections and their progress and [architecture.md](architecture.md) for the design.

---

## Section 7: Inventory views and alerts (complete)

**Status:** Section 7 is complete. Movement history can be read, stock on hand can be summed per product and warehouse, and low-stock and aging-stock alerts can be requested. Each product has its own reorder threshold. The React frontend and broader testing are not started.

**What we built.**

- `Contracts/InventoryViewContracts.cs` and `Endpoints/InventoryViewEndpoints.cs`: four read-only routes.
- `Models/Product.cs`, `Contracts/ProductContracts.cs`, `Endpoints/ProductEndpoints.cs`: each product now has a `ReorderThreshold`, accepted on create and update and returned in responses.
- `Data/Migrations/..._AddProductReorderThreshold`: the fifth migration. It adds one non-null `decimal(18,3)` column to `Products` with default 0 and a check that it is never negative. Existing products get 0 and no rows change otherwise.
- `InventoryViewEndpointTests.cs` and `ProductEndpointTests.cs`: 25 new tests in total.

**Endpoints.**

- `GET /api/stock-movements`: history, newest first (by `occurredAt`, then id). Filters: `productId`, `warehouseId`, `receiptBatchId`, `type` (exact name, such as `TransferOut`), `transferId`, `from`, `to` (inclusive). Paged with `skip` (default 0) and `take` (default 100, 1 to 500). The reply holds `total`, `skip`, `take`, and `items`, each with the product SKU, warehouse code, reason, and `transferId`.
- `GET /api/inventory/stock-levels`: quantity on hand and batch count per product and warehouse. Combinations with nothing on hand are omitted.
- `GET /api/inventory/alerts/low-stock`: active products whose total on hand is at or below their own saved `ReorderThreshold`. There is no global threshold or query parameter. Products with no stock at all are included, with 0. `warehouseId` counts only that warehouse against each product's threshold (400 if it does not exist). Each alert shows the threshold used. Lowest first.
- `POST /api/products` and `PUT /api/products/{id}` accept `reorderThreshold`: 0 to 1000000000, at most 3 decimal places, otherwise 400. On create it defaults to 0. On update, leaving it out keeps the current value.
- `GET /api/inventory/alerts/aging-stock`: batches with stock on hand received at least `days` ago (default 90, 0 to 36500). Filters: `productId`, `warehouseId`. Oldest first, with `ageDays`.

**Decisions.**

- The low-stock threshold is stored per product, because a sensible level differs between products. It is compared with `<=`, so the alert fires at the threshold, not only below it.
- The default for existing and new products is 0, which alerts only when a product is completely out of stock. That is the quietest safe choice: nobody is flooded with alerts until thresholds are set deliberately.
- A first version of this section used a global `?threshold=` query parameter with a default of 10. It was replaced by the stored per-product value.
- The migration is additive only (a new column with a default plus a check constraint), so existing products, batches, and movements are kept.
- Age is measured from `ReceivedAt`, which transfers preserve, so transferred stock keeps its true age. Emptied batches are not reported as aging.
- Low stock is judged per product (summed over batches), not per batch. Inactive products are skipped because they are retired; inactive warehouses and products still show in stock levels, aging, and history because the stock or history is real.
- Invalid filter values return 400 instead of being ignored.
- Responses are read-only projections, so nothing can change data through these routes.

**Plain-language explanations.**

- *Movement history*: the ledger of every receive, issue, adjustment, and transfer, which you can now read and filter.
- *Stock level*: how much of a product is in a warehouse right now.
- *Reorder threshold*: the quantity at or below which a product counts as running short. Each product has its own.
- *Low-stock alert*: a list of products running short, judged against each product's reorder threshold.
- *Aging stock*: stock that has sat for a long time and may need attention.
- *Paging*: returning a long list a page at a time.

**Key commands.**

```bash
dotnet dotnet-ef migrations add AddProductReorderThreshold --project backend/Inventory.Api --output-dir Data/Migrations
dotnet dotnet-ef database update --project backend/Inventory.Api
dotnet build InventoryManagement.slnx
dotnet test InventoryManagement.slnx
```

**Verification.**

- `dotnet build` succeeded with 0 warnings and 0 errors.
- `dotnet test` passed: 111 tests. The tests cover thresholds on create and update (including negative, more than 3 decimals, and omitted-on-update), alerts using each product's own threshold, the at-the-threshold case, the default of 0, and a threshold change taking effect.
- The endpoints were tested with the in-memory database only, not against the live API. The migration was applied to the local SQL Server with no drop or reset: the 2 products, 1 batch, and 7 movements were kept, both products got `ReorderThreshold` 0, and `__EFMigrationsHistory` lists five migrations.

**What is not done.** The React frontend and broader testing and polish. Sections 8 onward are not started.

---

## Section 6: Transfers (complete)

**Status:** Section 6 is complete. Stock can be moved from a chosen batch to another warehouse. The original receipt date is kept, and two movements are recorded. Movement-history viewing, alerts, and the frontend are not started.

**What we built.**

- `Models/StockMovement.cs`: two new movement types, `TransferOut` and `TransferIn`, and a nullable `TransferId` that both movements of one transfer share (it is empty for receive, issue, and adjust).
- `Data/Migrations/..._AddStockMovementTransferId`: the fourth migration. It only adds a nullable column and an index, so existing rows are kept (their `TransferId` stays empty).
- `Contracts/TransferContracts.cs` and `Endpoints/TransferEndpoints.cs`: the API route and validation.
- `TransferEndpointTests.cs`: 21 new tests.

**Endpoint.**

- `POST /api/stock/transfer` (body: `sourceReceiptBatchId`, `destinationWarehouseId`, `quantity`, optional `occurredAt`, optional `reference`) returns 200 with the `TransferId`, the `TransferOut` movement, the `TransferIn` movement, and the new destination batch.

**Rules.**

- The user names the source batch. The API never picks one (no FIFO).
- The stock arrives in a **new batch** in the destination warehouse. It copies the source batch's `ReceivedAt` (and reference), so stock age is unchanged. Its `QuantityReceived` is the amount transferred.
- Quantity must be greater than zero, with at most 3 decimal places, and cannot be more than the source batch has on hand (409).
- The destination must exist (400) and differ from the batch's current warehouse (400).
- The destination warehouse and the product must be active (409), as when receiving. The source warehouse may be inactive, so stock there can always be moved out.
- `occurredAt` defaults to now, cannot be in the future, and cannot be before the batch was received (400).
- The source change, the new batch, and both movements are saved together, so all happen or none do. The source batch's row version guards against two people changing it at once (409).
- Transferred batches can be issued from, adjusted, or transferred again, and always keep the original `ReceivedAt`.

**Decisions.**

- A new destination batch is created for every transfer rather than merging into an existing one, because merging would lose the separate dates.
- The two movements are linked by a shared `TransferId` (a GUID generated per transfer), so history can always pair them. It is a plain column with an index, not a separate Transfers table.
- As in Section 5, the in-memory test database does not enforce the row version.

**Plain-language explanations.**

- *Transfer*: moving stock from one warehouse to another. Nothing is bought or sold, so the stock keeps its original arrival date and is not treated as new.
- *Two movements*: the ledger shows stock leaving one place and arriving in the other, so totals still add up.
- *Transfer ID*: a unique label stamped on both ledger lines of one transfer, so they can be matched up later.
- *Migration*: this one adds a `TransferId` column and index to `StockMovements`. Nothing is dropped or reset.

**Key commands.**

```bash
dotnet dotnet-ef migrations add AddStockMovementTransferId --project backend/Inventory.Api --output-dir Data/Migrations
dotnet dotnet-ef database update --project backend/Inventory.Api
dotnet build InventoryManagement.slnx
dotnet test InventoryManagement.slnx
```

**Verification.**

- `dotnet build` succeeded with 0 warnings and 0 errors.
- `dotnet test` passed: 86 tests, including checks that both movements of a transfer share one `TransferId`, that separate transfers get different ids, and that other movements have none.
- The migration was applied to the local SQL Server with no drop or reset. `__EFMigrationsHistory` lists four migrations; the existing 1 batch and 7 movements were kept, all with an empty `TransferId`.
- The transfer endpoint itself was tested with the in-memory database only, not against the live API.

**What is not done.** Movement-history viewing, low-stock and aging alerts, and the React frontend. Sections 7 onward are not started.

---

## Section 5: Issuing and adjusting stock (complete)

**Status:** Section 5 is complete. Stock can be issued from a batch the user chooses, and a batch's quantity can be corrected with a recorded reason. Every operation records a stock movement. Transfers, movement-history viewing, alerts, and the frontend are not started.

**What we built.**

- `Models/StockMovement.cs`: two new movement types, `Issue` and `Adjust`, and an optional `Reason` field.
- `Models/ReceiptBatch.cs`: a `RowVersion` field used to detect two people changing the same batch at the same moment.
- `Contracts/StockOperationContracts.cs` and `Endpoints/StockOperationEndpoints.cs`: the API routes and validation.
- `Data/Migrations/..._AddStockMovementReasonAndBatchRowVersion`: the third migration. It only adds two columns.
- `StockOperationEndpointTests.cs`: 19 new tests.

**Endpoints.**

- `POST /api/stock/issue` (body: `receiptBatchId`, `quantity`, optional `occurredAt`, optional `reference`) removes stock from that batch. Returns 200 with the new movement and the quantity left on hand.
- `POST /api/stock/adjust` (body: `receiptBatchId`, `quantityChange`, `reason`, optional `occurredAt`, optional `reference`) corrects a batch up or down. `quantityChange` is signed.

**Rules.**

- The user names the batch. The API never picks one (no FIFO).
- Issue quantity must be greater than zero, with at most 3 decimal places, and cannot be more than the batch has on hand (409 otherwise).
- An adjustment cannot be zero, needs a non-blank reason, and cannot take the batch below zero or above the quantity originally received (409 otherwise). Extra stock found later should be received as a new batch, so it gets its own age.
- `occurredAt` defaults to now, cannot be in the future, and cannot be before the batch was received.
- Unknown batch is 400.
- The batch change and its movement are saved together, so either both happen or neither does.
- Issuing and adjusting are allowed even if the product or warehouse has been deactivated. Receiving into inactive records is blocked (Section 4), but stock that is already there must always be removable or correctable.
- Issuing never changes `QuantityReceived` or `ReceivedAt`, so stock age stays accurate.

**Decisions.**

- Adjustments are expressed as a signed change rather than "set the count to N", so the movement records exactly what changed.
- If two requests change the same batch at once, the second gets 409 ("changed by someone else, try again") instead of silently overwriting the first or overselling. The database already refuses a negative quantity as a further safeguard.
- The tests use an in-memory database, which does not enforce the row version, so that protection is not covered by automated tests; it relies on SQL Server.

**Plain-language explanations.**

- *Issue*: stock leaves the warehouse (for example, it is sold or used). The batch's on-hand quantity goes down and a ledger line is added.
- *Adjustment*: a correction, such as a damaged item or a miscount. It needs a reason so the history can be explained.
- *Row version*: a hidden counter the database changes on each save. If it has changed since the batch was read, the save is refused.
- *Migration*: this one adds a `Reason` column to `StockMovements` and a `RowVersion` column to `ReceiptBatches`. Existing rows are kept.

**Key commands.**

```bash
dotnet dotnet-ef migrations add AddStockMovementReasonAndBatchRowVersion --project backend/Inventory.Api --output-dir Data/Migrations
dotnet dotnet-ef database update --project backend/Inventory.Api
dotnet build InventoryManagement.slnx
dotnet test InventoryManagement.slnx
```

**Verification.**

- `dotnet build` succeeded with 0 warnings and 0 errors.
- `dotnet test` passed: 65 tests.
- The migration was applied to the local SQL Server with no drop or reset. The existing batch (40 units) and its movement were kept.
- Against the running API and real SQL Server: issuing 10 left 30 on hand, issuing 999 returned 409, an adjustment of +10 with a reason restored 40, and an adjustment without a reason returned 400. These checks left two extra movements (an issue and an adjustment) on sample batch 1; its quantity is back to 40.

**What is not done.** Transfers, movement-history viewing, low-stock and aging alerts, and the React frontend. Sections 6 onward are not started.

---

## Section 4: Receipt batches and receiving stock (complete)

**Status:** Section 4 is complete. Stock can be received into a warehouse as a batch, and every receipt records a stock movement. Transfers, movement-history viewing, and the frontend are not started.

**What we built.**

- `Models/ReceiptBatch.cs`: one quantity of one product received into one warehouse on one date, with quantity received, quantity on hand, an optional reference (such as a purchase order number), and timestamps.
- `Models/StockMovement.cs`: an append-only history record with a type (only `Receive` for now), product, warehouse, batch, a signed quantity change, and when it happened.
- `Contracts/ReceiptContracts.cs` and `Endpoints/ReceiptEndpoints.cs`: the API routes and validation.
- `Data/InventoryDbContext.cs`: the two new tables, quantity precision, foreign keys, and a database check that quantity on hand is never negative or above the quantity received.
- `Data/Migrations/..._AddReceiptBatchesAndStockMovements`: the second migration.
- `ReceiptEndpointTests.cs`: 14 new tests.

**Endpoints.**

- `POST /api/receipt-batches` receives stock (body: `productId`, `warehouseId`, `quantity`, optional `receivedAt`, optional `reference`) and returns 201.
- `GET /api/receipt-batches` lists batches, newest first; `?productId=` and `?warehouseId=` filter.
- `GET /api/receipt-batches/{id}` returns one, or 404.
- There is no edit or delete; `PUT` and `DELETE` return 405.

**Rules.**

- Quantity must be greater than zero, with at most 3 decimal places.
- `receivedAt` defaults to now. It may be in the past (to record an earlier delivery) but not in the future.
- The product and warehouse must exist (400 otherwise) and be active (409 otherwise). This settles the question left open in Section 3: inactive records keep their history but cannot receive new stock.
- Each receipt creates its batch and its `Receive` movement in a single database save, so either both exist or neither does.
- Each receipt always creates a new batch, even for the same product and warehouse, because batch age matters.

**Decisions.**

- There is no endpoint to view movements yet; that is Section 7. Tests read them straight from the database.
- Quantities use `decimal(18,3)` because units of measure may be fractional.
- Deleting products, warehouses, or batches that have history is blocked by the database (restricted foreign keys), matching the "never delete" rule.
- The 5-minute grace on future dates allows for small clock differences.

**Plain-language explanations.**

- *Receipt batch*: a "lot" of stock that arrived together. Keeping batches separate lets the app know how old each piece of stock is.
- *Stock movement*: a ledger line saying "this much stock came in (or later, went out) here". Lines are only added, never changed.
- *Transaction*: a group of database changes that succeed or fail together.
- *Migration*: this one only adds two new tables. It does not touch the existing `Products` and `Warehouses` data.

**Key commands.**

```bash
dotnet dotnet-ef migrations add AddReceiptBatchesAndStockMovements --project backend/Inventory.Api --output-dir Data/Migrations
dotnet dotnet-ef database update --project backend/Inventory.Api
dotnet build InventoryManagement.slnx
dotnet test InventoryManagement.slnx
```

**Verification.**

- `dotnet build` succeeded with 0 warnings and 0 errors.
- `dotnet test` passed: 46 tests.
- The migration was applied to the local SQL Server; the existing database was kept (no drop or reset). `__EFMigrationsHistory` lists `InitialCreate` and the new migration.
- Against the running API and real SQL Server: a receipt returned 201, a zero quantity 400, an inactive product 409, and the list returned the batch. The database then held 1 batch and 1 movement. These checks left sample product 2 (`S4-TEST`), sample warehouse 2 (`S4-WH`), and one batch of 40 units in the local database.

**What is not done.** Issuing and adjusting stock, transfers, movement-history viewing, low-stock and aging alerts, and the React frontend. Sections 5 onward are not started.

---

## Section 3: Products and warehouses (complete)

**Status:** Section 3, Products and warehouses, is complete. Products and warehouses can be created, listed, viewed, updated, and deactivated. Receipt batches, stock quantities, movements, and the frontend are not started.

**What we built.**

- `Models/Product.cs` and `Models/Warehouse.cs`: the two models, plain C# classes that become database tables. A product has an id, SKU, name, optional description, unit of measure (default `each`), an active flag, and created/updated timestamps. A warehouse has an id, code, name, optional location, an active flag, and timestamps.
- `Data/InventoryDbContext.cs`: now lists the `Products` and `Warehouses` tables and sets column sizes, required fields, and the unique indexes on `Sku` and `Code`.
- `Contracts/`: the shapes of the JSON the API accepts and returns, including the validation rules.
- `Endpoints/ProductEndpoints.cs` and `Endpoints/WarehouseEndpoints.cs`: the API routes.
- `Data/Migrations/`: the first EF Core migration, `InitialCreate`.
- `ProductEndpointTests.cs`, `WarehouseEndpointTests.cs`, `InventoryApiFactory.cs`: 30 new tests.

**Endpoints** (warehouses mirror products under `/api/warehouses`):

- `POST /api/products` creates one and returns 201.
- `GET /api/products` lists them, ordered by SKU; `?isActive=true|false` filters.
- `GET /api/products/{id}` returns one, or 404.
- `PUT /api/products/{id}` changes name, description, and unit of measure.
- `POST /api/products/{id}/deactivate` marks it inactive. Repeating it is harmless.
- There is no delete endpoint; `DELETE` returns 405.

**Validation.** Invalid requests get a 400 response that lists what is wrong.

- SKU (max 50) and warehouse code (max 20) are required and may contain only letters, digits, `.`, `_`, `-`, starting with a letter or digit.
- Name is required (max 200) and cannot be blank. Description (max 1000), unit of measure (max 20), and location (max 500) are optional.
- SKUs and codes are stored in upper case, so `abc-1` and `ABC-1` are the same. A duplicate gets 409 Conflict. This is checked in the API, and the database's unique index is a second safeguard if two requests race.

**Decisions.**

- **Deactivate, never delete.** Later sections will record stock and movements that point at products and warehouses. Keeping the rows means that history always makes sense. Inactive records still appear in the list unless you filter; later sections will decide how inactive records are treated when receiving or moving stock.
- **SKU and code cannot be edited.** They identify the record in the real world and in future history. A wrong one should be deactivated and re-created.
- **Whitespace in a SKU or code is rejected**, not trimmed. Names are trimmed.
- **Timestamps are UTC** (`datetimeoffset`).
- **Tests use an in-memory database**, so they are fast and need no SQL Server. The trade-off is that they do not exercise the real unique indexes or SQL Server behavior; those were checked manually (below).
- `Microsoft.EntityFrameworkCore.Design` and a local `dotnet-ef` tool (pinned in `dotnet-tools.json`) were added to create and apply migrations.

**Plain-language explanations.**

- *Model*: a C# description of one kind of record. EF Core turns each model into a table and each property into a column.
- *Validation*: checking input before using it, so bad data never reaches the database.
- *Endpoint*: a URL plus an HTTP verb (GET reads, POST creates or acts, PUT replaces fields) that the API responds to.
- *Unique index*: a database rule that refuses two rows with the same value, here the same SKU or code.
- *Migration*: a versioned script that builds or changes the database structure. `InitialCreate` creates the `Products` and `Warehouses` tables and the two unique indexes. EF records applied migrations in `__EFMigrationsHistory`, so each runs once. It only adds things; it does not drop or reset anything.

**Key commands.**

```bash
dotnet add backend/Inventory.Api package Microsoft.EntityFrameworkCore.Design --version 10.0.14
dotnet add tests/Inventory.Api.Tests package Microsoft.EntityFrameworkCore.InMemory --version 10.0.14
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.14
dotnet dotnet-ef migrations add InitialCreate --project backend/Inventory.Api --output-dir Data/Migrations
dotnet dotnet-ef database update --project backend/Inventory.Api
dotnet build InventoryManagement.slnx
dotnet test InventoryManagement.slnx
```

**Verification.**

- `dotnet build` succeeded with 0 warnings and 0 errors.
- `dotnet test` passed: 32 tests.
- The migration was applied to the local SQL Server. The `InventoryManagement` database did not exist yet, so it was created. `Products`, `Warehouses`, and `__EFMigrationsHistory` exist, with unique indexes `IX_Products_Sku` and `IX_Warehouses_Code`. No database was dropped or reset.
- Against the running API and real SQL Server: create (201), duplicate in different case (409), invalid input (400), update (200), deactivate (200), filtered list, get of a missing id (404), and `DELETE` (405) all behaved as described. These checks left one deactivated sample product and one deactivated sample warehouse (id 1 each) in the local database.

**Repository.** Section 3 is commit `4eb14b0` ("feat: add products and warehouses API with first migration"), which was pushed to GitHub at <https://github.com/kylepayne0110/inventory-management-app>.

**What is not done.** Receipt batches, receiving, issuing, adjusting, transfers, movements, inventory views and alerts, and the React frontend. Sections 4 onward are not started.

---

## Section 2: Database (complete)

**Status:** Section 2, Database, is complete. SQL Server runs locally in Docker and the API can connect to it. There are no tables, migrations, or inventory features yet.

**What we built.**

- `compose.yaml`: runs the official SQL Server 2022 Linux image with port 1433 published on `127.0.0.1` only, a named volume `sqlserver-data` mounted at `/var/opt/mssql`, and a health check.
- `.env.example`: a template with a fake placeholder password. The real `.env` is ignored by Git.
- `Data/InventoryDbContext.cs`: an empty EF Core `DbContext`. Tables are added in Section 3.
- `Program.cs`: registers the context and adds `GET /health/db`. `GET /health` is unchanged.
- `DatabaseHealthEndpointTests.cs`: checks that `/health/db` returns 503 with no password or host in the body when SQL Server is unreachable.

**How the API connects.** The API reads the connection string `ConnectionStrings:InventoryDb` from configuration. In local development it comes from .NET User Secrets; it could also come from the environment variable `ConnectionStrings__InventoryDb`. It is never in `appsettings*.json`. The connection uses the `sa` login, `localhost,1433`, and `TrustServerCertificate=True` (the container's certificate is self-signed, which is acceptable for local-only use).

**Decisions.**

- `MSSQL_SA_PASSWORD` is used, not the deprecated `SA_PASSWORD`. Compose refuses to start if it is unset.
- Port 1433 is bound to `127.0.0.1` so other machines on the network cannot reach the database.
- `/health/db` returns only `Healthy` (200) or `Unhealthy` (503) and swallows exception details so no credentials or server names leak. It gives up after 5 seconds.
- **User Secrets currently targets `master`.** .NET User Secrets holds a connection string with `Database=master`, the built-in system database, because the application database, `InventoryManagement`, does not exist yet and `master` always exists, so `/health/db` can confirm SQL Server is reachable. In Section 3, before applying the first migration, switch the connection string to `Database=InventoryManagement`; the first EF Core migration then creates the database and applies the schema.
- **No empty migration was created.** A migration with no tables records nothing useful. The first migration is created in Section 3, when the first real tables are introduced. The milestone plan was updated to say so.
- `System.Security.Cryptography.Pkcs` is temporarily pinned to 10.0.14 to resolve a package security advisory; the version pulled in by SQL Server's client library (9.0.11) triggered known-vulnerability build warnings.

**Plain-language explanations.**

- *Docker Compose* describes, in one file, how to run containers so one command (`docker compose up -d`) starts them the same way every time.
- *Named volume*: a storage area managed by Docker that outlives the container. Deleting or recreating the container keeps the data; `docker compose down -v` deletes it.
- *EF Core* translates between C# objects and database tables so the API does not hand-write most SQL.
- *DbContext*: the C# object that represents one conversation with the database. It will list the tables once they exist.
- *User Secrets*: a per-user store outside the repository (in your home folder) for development secrets such as passwords, so they are never committed.

**Verification.**

- Docker Compose configuration passed (`docker compose config`).
- The SQL Server container became healthy.
- `dotnet build` succeeded with 0 warnings and 0 errors.
- `dotnet test` passed: 2 xUnit tests.
- With SQL Server reachable, both `/health` and `/health/db` returned HTTP 200.

**Repository.** Section 2 is commit `01c2cc5` ("feat: connect API to local SQL Server"), which was pushed to GitHub at <https://github.com/kylepayne0110/inventory-management-app>.

**What is not done.** Products, warehouses, inventory entities, migrations, and the frontend. Sections 3 onward are not started.

---

## Section 1: API foundation (complete)

**Status:** Section 1, API foundation, is complete. Only the API foundation is built; there are no inventory features, database, frontend, or Docker setup yet.

**What we built.**

- `InventoryManagement.slnx`: the solution file at the repository root. It groups the projects so one command can build or test everything.
- `backend/Inventory.Api/`: the ASP.NET Core Web API project (.NET 10).
- `tests/Inventory.Api.Tests/`: the xUnit test project, which references the API project.

**What the API and endpoint do.** An API is a program that waits for requests over HTTP and sends back answers. This one has a single endpoint, `GET /health`. When you visit it, the API answers `200 OK` with `{"status":"Healthy"}`. That is a quick way to confirm the API started and is responding. Two documentation pages are also available while running in Development: the OpenAPI document at `/openapi/v1.json` (a machine-readable description of every endpoint) and Swagger UI at `/swagger` (a web page that shows that description and lets you try endpoints).

**How the files connect.**

- `Program.cs` in the API project starts the app, registers OpenAPI, turns on Swagger UI in Development, and defines `/health`.
- `Inventory.Api.csproj` lists the API's packages: `Microsoft.AspNetCore.OpenApi` (generates the OpenAPI document) and `Swashbuckle.AspNetCore.SwaggerUI` (the Swagger page, pointed at the generated document).
- `Inventory.Api.Tests.csproj` has a project reference to the API, plus `Microsoft.AspNetCore.Mvc.Testing`, which starts the API inside the test process without opening a real network port.
- `HealthEndpointTests.cs` uses that to call `/health` and check the status code and body. The last line of `Program.cs` (`public partial class Program;`) makes the API's entry point visible to the tests.

**Key commands.**

```bash
dotnet new sln -n InventoryManagement --format slnx
dotnet new webapi -n Inventory.Api -o backend/Inventory.Api --no-https
dotnet new xunit -n Inventory.Api.Tests -o tests/Inventory.Api.Tests
dotnet sln InventoryManagement.slnx add backend/Inventory.Api tests/Inventory.Api.Tests
dotnet add tests/Inventory.Api.Tests reference backend/Inventory.Api
dotnet add tests/Inventory.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add backend/Inventory.Api package Swashbuckle.AspNetCore.SwaggerUI
dotnet build InventoryManagement.slnx
dotnet test InventoryManagement.slnx
dotnet run --project backend/Inventory.Api
```

**Decisions.** .NET 10 ships its own OpenAPI generator but no Swagger UI, so a small Swashbuckle package supplies the UI page only. The template's sample weather endpoint was removed. HTTPS was left off because the app runs locally only.

**Verification.**

- `dotnet build InventoryManagement.slnx` succeeded.
- `dotnet test InventoryManagement.slnx` passed: the one xUnit test, which checks that `/health` returns 200 OK with a `Healthy` status.
- Manual HTTP checks against the running API confirmed that `/health`, the OpenAPI document at `/openapi/v1.json`, and Swagger UI at `/swagger` respond.

**Repository.** Section 1 is commit `60f0b79` ("feat: add .NET API foundation"), which was pushed to GitHub at <https://github.com/kylepayne0110/inventory-management-app>.

**What is not done.** Inventory entities, products, warehouses, EF Core, SQL Server, Docker, React, authentication, and integrations belong to later sections.

---

## Section 0: Foundation (complete)

**Status:** Section 0, Foundation, is complete. The documentation and repository foundation are in place; no application features are implemented.

**What we built.** Documentation and repository configuration files only: `README.md`, `SETUP.md`, `.gitignore`, `.editorconfig`, and the three files in `docs/`. There is no application code yet.

**Why.** Writing down the purpose, planned stack, and rules before coding gives later work a clear target and keeps the project understandable to readers who are not developers.

**Decisions.**

- Transfers will preserve the original receipt date, so stock age stays accurate.
- Issuing stock will not use FIFO; users will select which batch to issue from.
- SQL Server's data will be kept in a named Docker volume.
- The app will run locally only; there will be no deployment and no integrations.

**Plain-language notes.**

- *`.gitignore`* tells Git which files to leave out of the project history, such as passwords, downloaded libraries, and build output.
- *`.editorconfig`* tells editors to use consistent formatting (indentation, line endings) so code looks the same no matter who edits it.

**Repository.** The Git repository was initialized on the `main` branch and pushed to GitHub at <https://github.com/kylepayne0110/inventory-management-app>. The foundation commit is `0177c5e` ("docs: add inventory project foundation").

**Git steps in Section 0.** These are the steps that put the documentation foundation on GitHub:

1. **Initialization (`git init`).** Told Git to start tracking the project folder. Before this, the folder was ordinary files with no history.
2. **Branch rename (`git branch -m main`).** Named the main line of history `main`.
3. **Local author identity (`git config user.name` and `git config user.email`).** Recorded who is making commits. Without `--global`, this applies only to this project. A GitHub noreply email was used so the personal address stays private.
4. **First commit (`0177c5e`).** After checking status and staging the files (`git status`, `git add`, optionally `git diff --cached` to review), `git commit -m "docs: add inventory project foundation"` saved the first snapshot of the documentation and configuration files.
5. **Remote connection (`git remote add origin https://github.com/kylepayne0110/inventory-management-app`).** Linked the local folder to the empty GitHub repository. Nothing was uploaded yet.
6. **Successful push (`git push -u origin main`).** Uploaded the commit to GitHub. The `-u` flag linked local `main` to the remote `main`, so later pushes only need `git push`.

See the "Git and GitHub basics" section of [SETUP.md](../SETUP.md) for a reusable version of these steps. This work only covers documentation and version tracking; none of the app's features are built.

**What is not done.** `SETUP.md` is still an initial outline and will be completed as the app is built. All later sections are not started.
