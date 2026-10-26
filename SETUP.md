# Setup

> **PARTIALLY AVAILABLE.** The API and the local database connection are available: you can start SQL Server, run the API, and test it as described below. The product and warehouse tables, their API endpoints, and the first migration (Section 3) are available, as are receiving stock (Section 4) and issuing and adjusting stock (Section 5), which add two more migrations. Transfers (Section 6) add a fourth migration. Frontend setup is not available yet.

## Prerequisites (planned)

- Git
- .NET 10 SDK
- Node.js (LTS) and npm
- Docker (with Docker Compose) for the local SQL Server container
- An editor or IDE of your choice

Exact versions will be recorded here once they are confirmed.

## Git and GitHub basics

Git keeps a history of every change to the project. GitHub is the website that stores a shared copy of that history. The repository is at <https://github.com/kylepayne0110/inventory-management-app>.

### One-time setup for a new local folder

1. **Start tracking the folder.**

   ```bash
   git init
   ```

   This turns the current folder into a Git repository, so Git begins tracking its history.

2. **Name the branch `main`.**

   ```bash
   git branch -m main
   ```

   A branch is a line of history. This renames the current branch to `main`, the conventional name for the primary one.

3. **Set the author identity for this repository.**

   ```bash
   git config user.name "Your Name"
   git config user.email "your-email@example.com"
   ```

   Every commit records who made it. Leaving out `--global` keeps these settings local to this project only; your other repositories are unaffected. To keep a personal email address private, use the GitHub noreply address shown in your GitHub email settings (it looks like `<id>+<username>@users.noreply.github.com`).

4. **Connect the folder to GitHub.**

   ```bash
   git remote add origin https://github.com/kylepayne0110/inventory-management-app
   ```

   A remote is a copy of the repository hosted elsewhere. `origin` is the usual name for the main one. This command only records the address; it does not upload anything.

### Everyday workflow

```bash
git status
git add <files>
git commit -m "docs: describe the change"
git push
```

- `git status` shows which files are changed, staged, or untracked.
- `git add` stages files, meaning it marks them for inclusion in the next commit.
- `git commit -m "..."` saves the staged changes as a snapshot in the local history, with a short message describing it.
- `git push` uploads new local commits to GitHub.

### Optional: review what is staged before committing

```bash
git diff --cached
git diff --cached --check
git diff --cached --stat
```

"Cached" is another word for "staged". These commands look only at what `git add` has staged:

- `git diff --cached` shows the exact line-by-line changes that will go into the commit.
- `git diff --cached --check` is an optional check that warns about whitespace problems, such as trailing spaces.
- `git diff --cached --stat` is an optional summary listing each changed file and how many lines changed.

### The first push

```bash
git push -u origin main
```

The first time, Git needs to know where `main` should go. `-u` (short for `--set-upstream`) links the local `main` branch to `main` on `origin`, so later pushes can simply use `git push`.

Authentication is handled by GitHub's normal sign-in prompts or your configured credential helper. Never write passwords or access tokens into project files.

## Get the code (to be written)

Cloning instructions (`git clone https://github.com/kylepayne0110/inventory-management-app`) will be finalized here once the app setup steps below are written.

## Start the database

SQL Server runs in a Docker container defined in `compose.yaml`. All commands run from the repository root.

Plain-language terms:

- **Docker Compose** reads `compose.yaml`, which describes the containers to run, so one command starts them the same way every time.
- A **named volume** (`sqlserver-data`) is storage managed by Docker that outlives the container. You can stop or recreate the container and the database contents remain.
- **EF Core** (Entity Framework Core) is the library the API uses to talk to the database using C# objects instead of hand-written SQL.
- A **DbContext** (`InventoryDbContext`) is the C# object representing a session with the database. It lists the `Products` and `Warehouses` tables.
- **User Secrets** is a .NET feature that stores development secrets in a file in your home folder, outside the repository, so passwords are never committed.

### 1. Create the `.env` file

```bash
cp .env.example .env
```

Open `.env` in an editor and replace the placeholder with your own password: at least 8 characters with upper case, lower case, and a digit, and without `;` or `=`. `.env` is ignored by Git; never commit it.

### 2. Start and stop SQL Server

```bash
docker compose up -d      # start in the background (first run downloads the image)
docker compose ps         # wait until the status says "healthy"
docker compose stop       # stop, keeping the data
docker compose down       # remove the container, keeping the data volume
docker compose down -v    # remove the container AND delete the data (destructive)
```

### 3. Configure User Secrets

Load the password from `.env` into your shell, then store the connection string in User Secrets. The password is never written to a tracked file.

```bash
set -a; source .env; set +a
dotnet user-secrets set "ConnectionStrings:InventoryDb" "Server=localhost,1433;Database=master;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;Encrypt=True" --project backend/Inventory.Api
```

The connection string must use `Database=InventoryManagement`, the application database. (Before Section 3 it pointed at the built-in `master` database, because the application database did not exist yet. If your User Secrets still say `Database=master`, rerun the command above with `InventoryManagement`.) The same setting can instead be supplied as the environment variable `ConnectionStrings__InventoryDb`.

### 4. Apply the database migration

A **migration** is a versioned script, generated by EF Core, that builds or changes the database tables. The first one, `InitialCreate`, creates the `Products` and `Warehouses` tables and their unique indexes. Applying it also creates the `InventoryManagement` database if it does not exist. It only adds things; it never drops or resets existing data.

```bash
dotnet tool restore
dotnet dotnet-ef database update --project backend/Inventory.Api
```

`dotnet tool restore` installs `dotnet-ef`, the EF Core command-line tool, at the version pinned in `dotnet-tools.json`. EF records applied migrations in a table called `__EFMigrationsHistory`, so running the update again does nothing.

To create a new migration after changing the models in a later section: `dotnet dotnet-ef migrations add <Name> --project backend/Inventory.Api --output-dir Data/Migrations`.

## Run the API

Run these from the repository root. The .NET 10 SDK is required.

```bash
dotnet restore InventoryManagement.slnx
dotnet build InventoryManagement.slnx
dotnet run --project backend/Inventory.Api
```

- `dotnet restore` downloads the libraries the projects need.
- `dotnet build` compiles the API and the test project.
- `dotnet run` starts the API. It listens on <http://localhost:5127> (set in `backend/Inventory.Api/Properties/launchSettings.json`). Stop it with Ctrl+C.

With the API running (in the Development environment, which `dotnet run` uses by default), check both health endpoints from another terminal:

```bash
curl -i http://localhost:5127/health      # API is up: 200 {"status":"Healthy"}
curl -i http://localhost:5127/health/db   # SQL Server reachable: 200 {"status":"Healthy"}
```

If SQL Server is stopped or the secret is missing, `/health/db` returns 503 `{"status":"Unhealthy"}`. It deliberately gives no further detail, so no credentials leak.

Other pages:

- Swagger UI (interactive API documentation): <http://localhost:5127/swagger>
- OpenAPI document (raw JSON): <http://localhost:5127/openapi/v1.json>

### Try the products and warehouses endpoints

With the API running and the migration applied:

```bash
curl -i -X POST http://localhost:5127/api/products -H 'content-type: application/json' -d '{"sku":"BOLT-10","name":"M10 bolt","unitOfMeasure":"box"}'
curl -i http://localhost:5127/api/products
curl -i -X POST http://localhost:5127/api/warehouses -H 'content-type: application/json' -d '{"code":"MAIN","name":"Main warehouse","location":"Building 1"}'
curl -i -X POST http://localhost:5127/api/products/1/deactivate
```

Swagger UI at `/swagger` lists all of them and lets you try them in the browser. Expected errors: 400 for invalid input, 404 for an unknown id, 409 for a duplicate SKU or code, and 405 if you try `DELETE`.

## Run the frontend (not yet available)

Commands for installing dependencies and starting the React development server.

## Run the tests

```bash
dotnet test InventoryManagement.slnx
```

This builds the solution and runs the xUnit tests in `tests/Inventory.Api.Tests`. There are 32 tests. Two cover the health endpoints (`/health` returns 200 `Healthy`; `/health/db` returns 503 `Unhealthy` with no password or host when SQL Server is unreachable). The rest cover the product and warehouse endpoints: creating, validation errors, duplicate SKU/code, get, update, deactivate, and the absence of delete. The endpoint tests use an in-memory stand-in for the database, so none of the tests need SQL Server or a running API. The database-level unique indexes come from the migration and are not exercised by these tests.

## Troubleshooting (to be written)

Common problems and fixes will be collected here as they come up.
