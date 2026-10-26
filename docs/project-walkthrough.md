# Project Walkthrough

This is the build log for the inventory management app. It is updated at the end of each section of the project.

Each entry will capture:

- **What we built**: the concrete result of the section.
- **Why**: the reasoning behind it.
- **Important commands and decisions**: what was run, and what alternatives were considered.
- **Plain-language explanations**: short, non-jargon descriptions of the technologies involved, so a nontechnical reader can follow along.

See [milestones.md](milestones.md) for the list of sections and their progress and [architecture.md](architecture.md) for the design.

---

## Section 2: Database (in progress)

**Status:** Section 2 is in progress. SQL Server runs locally in Docker and the API can connect to it. There are no tables, migrations, or inventory features yet.

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
- The connection string currently targets the built-in `master` database, because the application database, `InventoryManagement`, does not exist yet. The order in Section 3 is: first change the User Secrets connection string to `Database=InventoryManagement`, then run the first EF Core migration, which creates the missing database and applies the schema.
- **No empty migration was created.** A migration with no tables records nothing useful. The first migration is created in Section 3, when the first real tables are introduced. The milestone plan was updated to say so.
- `System.Security.Cryptography.Pkcs` is pinned to 10.0.14 because the version pulled in by SQL Server's client library (9.0.11) triggered known-vulnerability build warnings.

**Plain-language explanations.**

- *Docker Compose* describes, in one file, how to run containers so one command (`docker compose up -d`) starts them the same way every time.
- *Named volume*: a storage area managed by Docker that outlives the container. Deleting or recreating the container keeps the data; `docker compose down -v` deletes it.
- *EF Core* translates between C# objects and database tables so the API does not hand-write most SQL.
- *DbContext*: the C# object that represents one conversation with the database. It will list the tables once they exist.
- *User Secrets*: a per-user store outside the repository (in your home folder) for development secrets such as passwords, so they are never committed.

**Verification.** `docker compose config` validated; the container became healthy; `dotnet build` succeeded with no warnings; `dotnet test` passed (2 tests); with the API running, `/health` and `/health/db` both returned 200 `{"status":"Healthy"}`.

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
